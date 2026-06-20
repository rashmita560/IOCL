Imports Microsoft.AspNetCore.Identity.EntityFrameworkCore
Imports Microsoft.EntityFrameworkCore
Imports IOCLCommunityHall.Models.Entities

Namespace Data
    Public Class ApplicationDbContext
        Inherits IdentityDbContext(Of ApplicationUser)

        Public Sub New(options As DbContextOptions(Of ApplicationDbContext))
            MyBase.New(options)
        End Sub

        Public Property CommunityHalls As DbSet(Of CommunityHall)
        Public Property HallBookings As DbSet(Of HallBooking)
        Public Property BookingFacilities As DbSet(Of BookingFacility)
        Public Property InventoryCategories As DbSet(Of InventoryCategory)
        Public Property InventoryItems As DbSet(Of InventoryItem)
        Public Property RentalRequests As DbSet(Of RentalRequest)
        Public Property RentalRequestItems As DbSet(Of RentalRequestItem)
        Public Property PriceHistories As DbSet(Of PriceHistory)
        Public Property Notifications As DbSet(Of Notification)
        Public Property AuditLogs As DbSet(Of AuditLog)
        Public Property InventoryTransactions As DbSet(Of InventoryTransaction)
        Public Property Employees As DbSet(Of Employee)

        Protected Overrides Sub OnModelCreating(builder As ModelBuilder)
            MyBase.OnModelCreating(builder)

            ' ── Employee Master ───────────────────────────────────────────────────
            builder.Entity(Of Employee)(Sub(e)
                e.HasKey(Function(emp) emp.EmployeeId)
                e.Property(Function(emp) emp.EmployeeId).HasMaxLength(8).IsRequired()
                e.Property(Function(emp) emp.EmployeeName).HasMaxLength(150).IsRequired()
                e.Property(Function(emp) emp.Department).HasMaxLength(150)
                e.Property(Function(emp) emp.Designation).HasMaxLength(150)
                e.Property(Function(emp) emp.Email).HasMaxLength(200)
                e.Property(Function(emp) emp.PhoneNumber).HasMaxLength(20)
                e.Property(Function(emp) emp.QuarterAddress).HasMaxLength(250)
            End Sub)

            ' ── ApplicationUser ──────────────────────────────────────────────────
            builder.Entity(Of ApplicationUser)(Sub(e)
                e.Property(Function(u) u.FullName).HasMaxLength(150)
                e.Property(Function(u) u.EmployeeId).HasMaxLength(8)
                e.Property(Function(u) u.Department).HasMaxLength(150)
                ' 1-to-1: ApplicationUser has one Employee via EmployeeId string FK
                e.HasOne(Function(u) u.Employee) _
                    .WithOne(Function(emp) emp.ApplicationUser) _
                    .HasForeignKey(Of ApplicationUser)(Function(u) u.EmployeeId) _
                    .HasPrincipalKey(Of Employee)(Function(emp) emp.EmployeeId) _
                    .OnDelete(DeleteBehavior.Restrict)
            End Sub)

            ' ── CommunityHall (Facility Master) ──────────────────────────────────
            builder.Entity(Of CommunityHall)(Sub(e)
                e.Property(Function(h) h.RentalRatePerDay).HasColumnType("decimal(10,2)")
                e.Property(Function(h) h.Name).HasMaxLength(150).IsRequired()
            End Sub)

            ' ── HallBooking ───────────────────────────────────────────────────────
            builder.Entity(Of HallBooking)(Sub(e)
                e.HasOne(Function(b) b.User).WithMany(Function(u) u.HallBookings) _
                    .HasForeignKey(Function(b) b.UserId).OnDelete(DeleteBehavior.Restrict)
                e.Property(Function(b) b.TotalAmount).HasColumnType("decimal(10,2)")
            End Sub)

            ' ── BookingFacility (join table) ──────────────────────────────────────
            builder.Entity(Of BookingFacility)(Sub(e)
                e.HasOne(Function(bf) bf.HallBooking).WithMany(Function(b) b.BookingFacilities) _
                    .HasForeignKey(Function(bf) bf.BookingId).OnDelete(DeleteBehavior.Cascade)
                e.HasOne(Function(bf) bf.Facility).WithMany(Function(h) h.HallBookings) _
                    .HasForeignKey(Function(bf) bf.FacilityId).OnDelete(DeleteBehavior.Restrict)
                e.Property(Function(bf) bf.RateAtBooking).HasColumnType("decimal(10,2)")
            End Sub)

            ' ── InventoryItem ────────────────────────────────────────────────────
            builder.Entity(Of InventoryItem)(Sub(e)
                e.HasOne(Function(i) i.Category).WithMany(Function(c) c.InventoryItems) _
                    .HasForeignKey(Function(i) i.CategoryId).OnDelete(DeleteBehavior.Restrict)
                e.Property(Function(i) i.CurrentPrice).HasColumnType("decimal(10,2)")
                e.Ignore(Function(i) i.AvailableQuantity)
            End Sub)

            ' ── RentalRequest ────────────────────────────────────────────────────
            builder.Entity(Of RentalRequest)(Sub(e)
                e.HasOne(Function(r) r.User).WithMany(Function(u) u.RentalRequests) _
                    .HasForeignKey(Function(r) r.UserId).OnDelete(DeleteBehavior.Restrict)
                e.Property(Function(r) r.GrandTotal).HasColumnType("decimal(12,2)")
                e.HasIndex(Function(r) r.RequestNumber).IsUnique()
                e.HasIndex(Function(r) r.CreatedAt)
            End Sub)

            ' ── RentalRequestItem ─────────────────────────────────────────────────
            builder.Entity(Of RentalRequestItem)(Sub(e)
                e.HasOne(Function(ri) ri.RentalRequest).WithMany(Function(r) r.RentalRequestItems) _
                    .HasForeignKey(Function(ri) ri.RentalRequestId).OnDelete(DeleteBehavior.Cascade)
                e.HasOne(Function(ri) ri.InventoryItem).WithMany(Function(i) i.RentalRequestItems) _
                    .HasForeignKey(Function(ri) ri.InventoryItemId).OnDelete(DeleteBehavior.Restrict)
                e.Property(Function(ri) ri.UnitPriceAtRequest).HasColumnType("decimal(10,2)")
                e.Ignore(Function(ri) ri.LineTotal)
            End Sub)

            ' ── PriceHistory ──────────────────────────────────────────────────────
            builder.Entity(Of PriceHistory)(Sub(e)
                e.HasOne(Function(p) p.InventoryItem).WithMany(Function(i) i.PriceHistories) _
                    .HasForeignKey(Function(p) p.InventoryItemId).OnDelete(DeleteBehavior.Restrict)
                e.Property(Function(p) p.PreviousPrice).HasColumnType("decimal(10,2)")
                e.Property(Function(p) p.UpdatedPrice).HasColumnType("decimal(10,2)")
                e.Ignore(Function(p) p.PriceDifference)
                e.Ignore(Function(p) p.PercentageChange)
            End Sub)

            ' ── Notification ──────────────────────────────────────────────────────
            builder.Entity(Of Notification)(Sub(e)
                e.HasOne(Function(n) n.User).WithMany(Function(u) u.Notifications) _
                    .HasForeignKey(Function(n) n.UserId).OnDelete(DeleteBehavior.Cascade)
            End Sub)

            ' ── AuditLog ──────────────────────────────────────────────────────────
            builder.Entity(Of AuditLog)(Sub(e)
                e.HasOne(Function(a) a.User).WithMany(Function(u) u.AuditLogs) _
                    .HasForeignKey(Function(a) a.UserId).OnDelete(DeleteBehavior.Restrict)
            End Sub)

            ' ── InventoryTransaction ──────────────────────────────────────────────
            builder.Entity(Of InventoryTransaction)(Sub(e)
                e.HasOne(Function(t) t.InventoryItem).WithMany(Function(i) i.InventoryTransactions) _
                    .HasForeignKey(Function(t) t.InventoryItemId).OnDelete(DeleteBehavior.Restrict)
            End Sub)
        End Sub
    End Class
End Namespace
