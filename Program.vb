Imports Microsoft.AspNetCore.Builder
Imports Microsoft.AspNetCore.Identity
Imports Microsoft.EntityFrameworkCore
Imports Microsoft.Extensions.DependencyInjection
Imports Microsoft.Extensions.Hosting
Imports Microsoft.Extensions.Logging
Imports IOCLCommunityHall.Data
Imports IOCLCommunityHall.Models.Entities
Imports IOCLCommunityHall.Repositories
Imports IOCLCommunityHall.Services
Imports Microsoft.Extensions.Configuration

Module Program
    Sub Main(args As String())
        Dim builder = WebApplication.CreateBuilder(args)

        ' ─── Database ───────────────────────────────────────────────────────────
        Dim connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        builder.Services.AddDbContext(Of ApplicationDbContext)(
            Sub(options)
                options.UseSqlite(connectionString)
            End Sub)

        ' ─── Identity ───────────────────────────────────────────────────────────
        builder.Services.AddIdentity(Of ApplicationUser, IdentityRole)(Sub(options)
            options.Password.RequireDigit = True
            options.Password.RequireLowercase = True
            options.Password.RequireUppercase = False
            options.Password.RequiredLength = 6
            options.Password.RequireNonAlphanumeric = False
            options.SignIn.RequireConfirmedAccount = False
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15)
            options.Lockout.MaxFailedAccessAttempts = 5
        End Sub) _
        .AddEntityFrameworkStores(Of ApplicationDbContext)() _
        .AddDefaultTokenProviders()

        ' ─── Cookie Config ──────────────────────────────────────────────────────
        builder.Services.ConfigureApplicationCookie(Sub(options)
            options.LoginPath = "/Account/Login"
            options.LogoutPath = "/Account/Logout"
            options.AccessDeniedPath = "/Account/AccessDenied"
            options.ExpireTimeSpan = TimeSpan.FromHours(8)
            options.SlidingExpiration = True
        End Sub)

        ' ─── MVC ────────────────────────────────────────────────────────────────
        builder.Services.AddControllersWithViews(Sub(options)
            options.Filters.Add(GetType(IOCLCommunityHall.Controllers.NotificationActionFilter))
        End Sub).AddRazorRuntimeCompilation()
        builder.Services.AddHttpContextAccessor()
        builder.Services.AddSession(Sub(options)
            options.IdleTimeout = TimeSpan.FromHours(8)
            options.Cookie.HttpOnly = True
            options.Cookie.IsEssential = True
        End Sub)

        ' ─── Repositories ───────────────────────────────────────────────────────
        builder.Services.AddScoped(GetType(IGenericRepository(Of )), GetType(GenericRepository(Of )))
        builder.Services.AddScoped(Of IRentalRequestRepository, RentalRequestRepository)()
        builder.Services.AddScoped(Of IInventoryRepository, InventoryRepository)()

        ' ─── Services ───────────────────────────────────────────────────────────
        builder.Services.AddScoped(Of IRentalService, RentalService)()
        builder.Services.AddScoped(Of IInventoryService, InventoryService)()
        builder.Services.AddScoped(Of IReportService, ReportService)()
        builder.Services.AddScoped(Of IAuditService, AuditService)()
        builder.Services.AddScoped(Of INotificationService, NotificationService)()

        ' ─── Autonomous Background Services ─────────────────────────────────────
        ' Runs hourly: auto-releases inventory stock when rental EndDate has passed
        builder.Services.AddHostedService(Of InventoryReleaseService)()

        Dim app = builder.Build()

        ' ─── Seed Database ──────────────────────────────────────────────────────
        Using scope = app.Services.CreateScope()
            Dim services = scope.ServiceProvider
            Try
                Dim context = services.GetRequiredService(Of ApplicationDbContext)()
                context.Database.EnsureCreated()
                ReconcileReservedQuantities(context)
                SeedData.InitializeAsync(services).GetAwaiter().GetResult()
            Catch ex As Exception
                Dim logger = services.GetRequiredService(Of ILoggerFactory)().CreateLogger("Program")
                logger.LogError(ex, "An error occurred seeding the database.")
            End Try
        End Using

        ' ─── Middleware Pipeline ─────────────────────────────────────────────────
        If Not app.Environment.IsDevelopment() Then
            app.UseExceptionHandler("/Home/Error")
            app.UseHsts()
        End If

        app.UseStaticFiles()
        app.UseRouting()
        app.UseSession()
        app.UseAuthentication()
        app.UseAuthorization()

        app.MapControllerRoute(
            name:="default",
            pattern:="{controller=Home}/{action=Index}/{id?}")

        app.Run()
    End Sub

    ''' <summary>
    ''' At startup: (1) auto-releases any expired allocations whose EndDate has already
    ''' passed (filling the gap between last server run and now), then (2) recalculates
    ''' ReservedQuantity for every item from the InventoryAllocations table.
    ''' This ensures the DB is always consistent regardless of how long the server was offline.
    ''' </summary>
    Private Sub ReconcileReservedQuantities(context As ApplicationDbContext)
        Dim today = DateTime.UtcNow.Date

        ' Step 1 ── Release any allocations whose rental period has already ended
        Dim expired = context.InventoryAllocations.
            Where(Function(a) (a.Status = "Approved" OrElse a.Status = "Reserved") AndAlso
                               a.EndDate.Date < today).
            ToList()

        For Each alloc In expired
            alloc.Status = "Released"

            ' Update linked request to Returned if still Approved/Pending
            Dim req = context.RentalRequests.Find(alloc.RequestId)
            If req IsNot Nothing AndAlso
               (req.Status = Models.Entities.RequestStatus.Approved OrElse
                req.Status = Models.Entities.RequestStatus.Pending) Then
                req.Status = Models.Entities.RequestStatus.Returned
                req.ReviewedAt = DateTime.UtcNow
                req.ReviewedByEmployeeId = "SYSTEM"
            End If

            ' Log the startup release as a transaction
            context.InventoryTransactions.Add(New Models.Entities.InventoryTransaction With {
                .InventoryItemId = alloc.InventoryItemId,
                .RentalRequestId = alloc.RequestId,
                .TransactionType = Models.Entities.TransactionType.Release,
                .QuantityChanged = alloc.AllocatedQuantity,
                .QuantityBefore = 0,   ' Will be corrected in Step 2 reconcile
                .QuantityAfter = 0,
                .Notes = $"Startup reconcile: expired allocation released (EndDate {alloc.EndDate:dd MMM yyyy}).",
                .PerformedBy = "SYSTEM (Startup Reconcile)"
            })
        Next

        If expired.Any() Then
            context.SaveChanges()
        End If

        ' Step 2 ── Recalculate ReservedQuantity for ALL items from the remaining
        '           active (Approved/Reserved) allocations — single source of truth
        Dim items = context.InventoryItems.Where(Function(i) i.IsActive).ToList()
        For Each item In items
            Dim reserved = context.InventoryAllocations.
                Where(Function(a) a.InventoryItemId = item.Id AndAlso
                                  (a.Status = "Approved" OrElse a.Status = "Reserved")).
                Sum(Function(a) CType(a.AllocatedQuantity, Integer?))
            item.ReservedQuantity = If(reserved, 0)
        Next
        context.SaveChanges()
    End Sub
End Module
