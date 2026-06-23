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
        ' Core release engine (scoped) — called on page load AND by background job
        builder.Services.AddScoped(Of IInventoryReleaseEngine, InventoryReleaseEngine)()

        ' ─── Autonomous Background Services ─────────────────────────────────────
        ' Runs hourly: delegates to IInventoryReleaseEngine for the actual release logic
        builder.Services.AddHostedService(Of InventoryReleaseService)()

        Dim app = builder.Build()

        ' ─── Seed Database ──────────────────────────────────────────────────────
        Using scope = app.Services.CreateScope()
            Dim services = scope.ServiceProvider
            Try
                Dim context = services.GetRequiredService(Of ApplicationDbContext)()
                context.Database.EnsureCreated()
                ReconcileReservedQuantities(context)
                ' Run the release engine immediately so any rentals that ended
                ' while the server was offline are released on first startup.
                Dim releaseEngine = services.GetRequiredService(Of IInventoryReleaseEngine)()
                releaseEngine.TriggerReleaseAsync().GetAwaiter().GetResult()
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
    ''' At startup:
    '''   1. Idempotent ALTER TABLE — adds InventoryReleased / InventoryReleasedAt columns
    '''      if they don't already exist (EnsureCreated does not run migrations).
    '''   2. Releases expired allocations where release time has arrived
    '''      (EndDate + 1 day + 06:00 AM UTC) and InventoryReleased = False.
    '''   3. Recalculates ReservedQuantity for every item from live allocation data.
    '''
    ''' Keeps the DB consistent regardless of how long the server was offline.
    ''' NOTE: request.Status is NOT changed — stays Approved per business rule.
    ''' </summary>
    Private Sub ReconcileReservedQuantities(context As ApplicationDbContext)
        ' ── Step 0: Idempotent SQL migration ────────────────────────────────────────
        ' SQLite doesn't support IF NOT EXISTS on ALTER TABLE; catch the error instead.
        Try
            context.Database.ExecuteSqlRaw(
                "ALTER TABLE RentalRequests ADD COLUMN InventoryReleased INTEGER NOT NULL DEFAULT 0")
        Catch
            ' Column already exists — safe to ignore
        End Try
        Try
            context.Database.ExecuteSqlRaw(
                "ALTER TABLE RentalRequests ADD COLUMN InventoryReleasedAt TEXT NULL")
        Catch
            ' Column already exists — safe to ignore
        End Try

        Dim utcNow = DateTime.UtcNow
        Dim istZone As TimeZoneInfo
        Try
            istZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time")
        Catch
            istZone = TimeZoneInfo.CreateCustomTimeZone("IST", New TimeSpan(5, 30, 0), "India Standard Time", "India Standard Time")
        End Try
        Dim istNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, istZone)

        ' ── Step 1: Release expired, unreleased Approved allocations ─────────────────
        ' Release condition: istNow >= EndDate + 1 day + 06:00 AM IST
        '                    AND request is Approved AND InventoryReleased = False
        Dim activeAllocations = context.InventoryAllocations.
            Where(Function(a) a.Status = "Approved" OrElse a.Status = "Reserved").
            ToList()

        Dim byRequest = activeAllocations.GroupBy(Function(a) a.RequestId).ToList()

        For Each group In byRequest
            Dim req = context.RentalRequests.Find(group.Key)
            If req Is Nothing Then Continue For
            If req.Status <> Models.Entities.RequestStatus.Approved Then Continue For
            If req.InventoryReleased Then Continue For   ' Already released — skip

            ' Check release time: EndDate + 1 day + 06:00 AM IST
            Dim releaseTime = req.EndDate.Date.AddDays(1).AddHours(6)
            If istNow < releaseTime Then Continue For

            For Each alloc In group
                Dim item = context.InventoryItems.Find(alloc.InventoryItemId)
                If item Is Nothing Then Continue For

                Dim qtyReleased = alloc.AllocatedQuantity
                Dim reservedBefore = item.ReservedQuantity

                ' FORMULA: ReservedQty = MAX(0, ReservedQty - AllocatedQty)
                item.ReservedQuantity = Math.Max(0, item.ReservedQuantity - qtyReleased)
                item.UpdatedAt = utcNow
                alloc.Status = "Released"

                context.InventoryTransactions.Add(New Models.Entities.InventoryTransaction With {
                    .InventoryItemId = alloc.InventoryItemId,
                    .RentalRequestId = group.Key,
                    .TransactionType = Models.Entities.TransactionType.Release,
                    .QuantityChanged = qtyReleased,
                    .QuantityBefore = reservedBefore,
                    .QuantityAfter = item.ReservedQuantity,
                    .Notes = $"Startup reconcile: expired allocation released " &
                             $"(EndDate {req.EndDate:dd-MMM-yyyy}). Request #{req.RequestNumber}.",
                    .PerformedBy = "SYSTEM (Startup Reconcile)",
                    .CreatedAt = utcNow
                })
            Next

            ' Mark released — do NOT change request.Status (stays Approved per business rule)
            req.InventoryReleased = True
            req.InventoryReleasedAt = utcNow
        Next

        context.SaveChanges()

        ' ── Step 2: Recalculate ReservedQuantity for ALL items ───────────────────────
        ' Single source of truth: sum of active (Approved/Reserved) allocations per item.
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
