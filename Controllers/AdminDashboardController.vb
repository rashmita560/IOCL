Imports Microsoft.AspNetCore.Mvc
Imports Microsoft.AspNetCore.Authorization
Imports Microsoft.AspNetCore.Identity
Imports Microsoft.EntityFrameworkCore
Imports System.Text.Json
Imports IOCLCommunityHall.Data
Imports IOCLCommunityHall.Models.Entities
Imports IOCLCommunityHall.Models.ViewModels
Imports IOCLCommunityHall.Services

Namespace Controllers
    <Authorize(Roles:="SuperAdmin")>
    Public Class AdminDashboardController
        Inherits Controller

        Private ReadOnly _context As ApplicationDbContext
        Private ReadOnly _inventoryService As IInventoryService
        Private ReadOnly _notificationService As INotificationService
        Private ReadOnly _userManager As UserManager(Of ApplicationUser)

        Public Sub New(context As ApplicationDbContext,
                       inventoryService As IInventoryService,
                       notificationService As INotificationService,
                       userManager As UserManager(Of ApplicationUser))
            _context = context
            _inventoryService = inventoryService
            _notificationService = notificationService
            _userManager = userManager
        End Sub

        Public Async Function Index() As Task(Of IActionResult)
            Dim currentUser As ApplicationUser = Await _userManager.GetUserAsync(Me.User)
            Dim vm = New DashboardViewModel()

            ' ── KPI Cards ────────────────────────────────────────────────────────
            vm.TotalRequests = Await _context.RentalRequests.CountAsync()
            vm.PendingRequests = Await _context.RentalRequests.CountAsync(Function(r) r.Status = RequestStatus.Pending)
            vm.ApprovedRequests = Await _context.RentalRequests.CountAsync(Function(r) r.Status = RequestStatus.Approved)
            vm.RejectedRequests = Await _context.RentalRequests.CountAsync(Function(r) r.Status = RequestStatus.Rejected)
            vm.TotalRevenue = CDec(Await _context.RentalRequests.Where(Function(r) r.Status = RequestStatus.Approved).SumAsync(Function(r) CDbl(r.GrandTotal)))
            vm.MonthlyRevenue = CDec(Await _context.RentalRequests.
                Where(Function(r) r.Status = RequestStatus.Approved AndAlso r.CreatedAt.Month = DateTime.Now.Month AndAlso r.CreatedAt.Year = DateTime.Now.Year).
                SumAsync(Function(r) CDbl(r.GrandTotal)))
            vm.TotalInventoryItems = Await _context.InventoryItems.CountAsync(Function(i) i.IsActive)
            vm.TotalHallBookings = Await _context.HallBookings.CountAsync()
            vm.PendingHallBookings = Await _context.HallBookings.CountAsync(Function(b) b.Status = BookingStatus.Pending)
            vm.LowStockItemCount = (Await _inventoryService.GetLowStockItemsAsync()).Count()

            ' ── Monthly Revenue Chart (last 6 months) ────────────────────────────
            For i = 5 To 0 Step -1
                Dim targetDate = DateTime.Now.AddMonths(-i)
                vm.MonthlyRevenueLabels.Add(targetDate.ToString("MMM yy"))
                Dim rev = CDec(Await _context.RentalRequests.
                    Where(Function(r) r.Status = RequestStatus.Approved AndAlso
                                       r.CreatedAt.Month = targetDate.Month AndAlso
                                       r.CreatedAt.Year = targetDate.Year).
                    SumAsync(Function(r) CDbl(r.GrandTotal)))
                vm.MonthlyRevenueData.Add(rev)
            Next

            ' ── Booking Trend Chart (last 6 months) ──────────────────────────────
            For i = 5 To 0 Step -1
                Dim targetDate = DateTime.Now.AddMonths(-i)
                vm.BookingTrendLabels.Add(targetDate.ToString("MMM yy"))
                Dim cnt = Await _context.RentalRequests.
                    CountAsync(Function(r) r.CreatedAt.Month = targetDate.Month AndAlso r.CreatedAt.Year = targetDate.Year)
                vm.BookingTrendData.Add(cnt)
            Next

            ' ── Top 5 Most Used Items ─────────────────────────────────────────────
            Dim topItems = Await _context.RentalRequestItems.
                Include(Function(ri) ri.InventoryItem).
                GroupBy(Function(ri) ri.InventoryItem.Name).
                Select(Function(g) New With {.Name = g.Key, .Total = g.Sum(Function(ri) ri.RequestedQuantity)}).
                OrderByDescending(Function(x) x.Total).
                Take(5).
                ToListAsync()
            vm.TopItemNames = topItems.Select(Function(x) x.Name).ToList()
            vm.TopItemUsage = topItems.Select(Function(x) x.Total).ToList()

            Dim totalQty = Await _context.InventoryItems.Where(Function(i) i.IsActive).SumAsync(Function(i) i.TotalQuantity)
            Dim reservedQty = Await _context.InventoryItems.Where(Function(i) i.IsActive).SumAsync(Function(i) i.ReservedQuantity)
            vm.InventoryStatusLabels = New List(Of String) From {"Available", "Reserved"}
            vm.InventoryStatusData = New List(Of Integer) From {totalQty - reservedQty, reservedQty}

            ' ── Recent Activity ───────────────────────────────────────────────────
            vm.RecentRequests = Await _context.RentalRequests.
                Include(Function(r) r.User).
                OrderByDescending(Function(r) r.CreatedAt).
                Take(5).ToListAsync()
            vm.RecentHallBookings = Await _context.HallBookings.
                Include(Function(b) b.BookingFacilities).ThenInclude(Function(bf) bf.Facility).
                Include(Function(b) b.User).
                OrderByDescending(Function(b) b.CreatedAt).
                Take(5).ToListAsync()
            vm.LowStockItems = (Await _inventoryService.GetLowStockItemsAsync()).ToList()

            ViewBag.MonthlyRevenueJson = JsonSerializer.Serialize(vm.MonthlyRevenueData)
            ViewBag.MonthlyLabelsJson = JsonSerializer.Serialize(vm.MonthlyRevenueLabels)
            ViewBag.BookingTrendJson = JsonSerializer.Serialize(vm.BookingTrendData)
            ViewBag.BookingLabelsJson = JsonSerializer.Serialize(vm.BookingTrendLabels)
            ViewBag.TopItemsJson = JsonSerializer.Serialize(vm.TopItemUsage)
            ViewBag.TopItemsLabelsJson = JsonSerializer.Serialize(vm.TopItemNames)
            ViewBag.InvStatusJson = JsonSerializer.Serialize(vm.InventoryStatusData)
            ViewBag.InvStatusLabelsJson = JsonSerializer.Serialize(vm.InventoryStatusLabels)

            Return View(vm)
        End Function
    End Class

    ''' <summary>
    ''' Handles rental request approval workflow for all approver roles: HOD, GM, SuperAdmin (HR).
    ''' Each role only sees requests at their current approval stage.
    ''' Self-approval is always blocked.
    ''' </summary>
    <Authorize(Roles:="SuperAdmin,HOD,GM")>
    Public Class AdminRequestController
        Inherits Controller

        Private ReadOnly _rentalService As IRentalService
        Private ReadOnly _hallBookingService As IHallBookingService
        Private ReadOnly _userManager As UserManager(Of ApplicationUser)
        Private ReadOnly _auditService As IAuditService
        Private ReadOnly _notificationService As INotificationService

        Public Sub New(rentalService As IRentalService,
                       hallBookingService As IHallBookingService,
                       userManager As UserManager(Of ApplicationUser),
                       auditService As IAuditService,
                       notificationService As INotificationService)
            _rentalService = rentalService
            _hallBookingService = hallBookingService
            _userManager = userManager
            _auditService = auditService
            _notificationService = notificationService
        End Sub

        Public Async Function Index(statusFilter As String, search As String) As Task(Of IActionResult)
            Dim currentUser As ApplicationUser = Await _userManager.GetUserAsync(Me.User)
            Dim roles = Await _userManager.GetRolesAsync(currentUser)
            Dim currentRole = If(roles.FirstOrDefault(), "User")

            Dim requests As IEnumerable(Of RentalRequest)

            If currentRole = "SuperAdmin" Then
                ' HR (SuperAdmin) only sees requests that:
                '   - Have GrandTotal <= ₹10,000
                '   - Are NOT submitted by GM (since GM is auto-approved)
                '   - Are NOT pending HOD approval (must be approved by HOD first)
                '   - Are NOT pending GM approval (high-value requests)
                Dim allHR = Await _rentalService.GetAllRequestsAsync()
                requests = allHR.Where(Function(r) r.GrandTotal <= 10000D AndAlso
                                                   r.SubmittedByRole <> "GM" AndAlso
                                                   r.ApprovalStage <> ApprovalStage.PendingHOD AndAlso
                                                   r.ApprovalStage <> ApprovalStage.PendingGM)
            ElseIf currentRole = "HOD" Then
                ' HOD sees ALL requests for full visibility (same as SuperAdmin panel).
                ' HOD can only approve requests that are at PendingHOD stage (enforced on Details).
                requests = Await _rentalService.GetAllRequestsAsync()
            ElseIf currentRole = "GM" Then
                ' GM sees:
                '   - All requests that route through the GM approval path (GrandTotal > ₹10,000 and not submitted by GM)
                '   - Plus their own requests (GM's self requests, which are auto-approved)
                Dim allGM = Await _rentalService.GetAllRequestsAsync()
                requests = allGM.Where(Function(r) r.SubmittedByRole = "GM" OrElse
                                                   (r.GrandTotal > 10000D AndAlso r.SubmittedByRole <> "GM"))
            Else
                ' Any other role falls through to stage-based filter
                requests = Await _rentalService.GetRequestsForApproverAsync(currentUser.Id, currentRole)
            End If

            If Not String.IsNullOrEmpty(statusFilter) AndAlso statusFilter <> "All" Then
                Dim status As RequestStatus
                If [Enum].TryParse(statusFilter, status) Then
                    requests = requests.Where(Function(r) r.Status = status)
                End If
            End If

            If Not String.IsNullOrEmpty(search) Then
                requests = requests.Where(Function(r) r.RequestNumber.Contains(search, StringComparison.OrdinalIgnoreCase) OrElse
                                                       r.User?.FullName.Contains(search, StringComparison.OrdinalIgnoreCase))
            End If

            ViewBag.StatusFilter = statusFilter
            ViewBag.Search = search
            ViewBag.CurrentRole = currentRole
            ViewBag.FCFSQueue = (Await _rentalService.GetFCFSQueueAsync()).Take(10).ToList()
            Return View(requests.ToList())
        End Function

        Public Async Function Details(id As Integer) As Task(Of IActionResult)
            Dim request = Await _rentalService.GetRequestByIdAsync(id)
            If request Is Nothing Then Return NotFound()

            Dim currentUser As ApplicationUser = Await _userManager.GetUserAsync(Me.User)
            Dim roles = Await _userManager.GetRolesAsync(currentUser)
            Dim currentRole = If(roles.FirstOrDefault(), "User")

            ViewBag.CurrentUserId = currentUser.Id
            ViewBag.CurrentRole = currentRole
            ViewBag.IsSelfRequest = (request.UserId = currentUser.Id)

            Return View(request)
        End Function

        <HttpPost>
        <ValidateAntiForgeryToken>
        Public Async Function Approve(id As Integer) As Task(Of IActionResult)
            Dim currentUser As ApplicationUser = Await _userManager.GetUserAsync(Me.User)
            Dim roles = Await _userManager.GetRolesAsync(currentUser)
            Dim currentRole = If(roles.FirstOrDefault(), "User")

            Dim result = Await _rentalService.ApproveRequestAsync(id, currentUser.Id, currentRole)

            Await _auditService.LogAsync(currentUser.Id, "Approve", "RentalRequest", id.ToString(),
                $"Request stage approved by {currentRole} ({currentUser.UserName}). {result.Message}", "", currentRole & " Approved",
                HttpContext.Connection.RemoteIpAddress?.ToString())

            If result.Success Then
                TempData("Success") = $"✓ {result.Message}"
            Else
                TempData("Error") = result.Message
            End If
            Return RedirectToAction("Details", New With {.id = id})
        End Function

        <HttpPost>
        <ValidateAntiForgeryToken>
        Public Async Function Reject(id As Integer, reason As String) As Task(Of IActionResult)
            Dim currentUser As ApplicationUser = Await _userManager.GetUserAsync(Me.User)
            If String.IsNullOrEmpty(reason) Then
                TempData("Error") = "Rejection reason is required."
                Return RedirectToAction("Details", New With {.id = id})
            End If
            Dim success = Await _rentalService.RejectRequestAsync(id, reason, currentUser.UserName)
            Await _auditService.LogAsync(currentUser.Id, "Reject", "RentalRequest", id.ToString(),
                $"Request rejected. Reason: {reason}", "", "Rejected",
                HttpContext.Connection.RemoteIpAddress?.ToString())
            TempData(If(success, "Success", "Error")) = If(success, "Request rejected.", "Could not reject request.")
            Return RedirectToAction("Details", New With {.id = id})
        End Function

        ' Hall Booking actions (SuperAdmin only)
        <Authorize(Roles:="SuperAdmin")>
        <HttpPost>
        <ValidateAntiForgeryToken>
        Public Async Function ApproveHallBooking(id As Integer) As Task(Of IActionResult)
            Dim adminUser As ApplicationUser = Await _userManager.GetUserAsync(Me.User)
            Await _hallBookingService.ApproveBookingAsync(id, adminUser.UserName)
            TempData("Success") = "Hall booking approved."
            Return RedirectToAction("HallBookings")
        End Function

        <Authorize(Roles:="SuperAdmin")>
        <HttpPost>
        <ValidateAntiForgeryToken>
        Public Async Function RejectHallBooking(id As Integer, reason As String) As Task(Of IActionResult)
            Dim adminUser As ApplicationUser = Await _userManager.GetUserAsync(Me.User)
            Await _hallBookingService.RejectBookingAsync(id, reason, adminUser.UserName)
            TempData("Success") = "Hall booking rejected."
            Return RedirectToAction("HallBookings")
        End Function

        <Authorize(Roles:="SuperAdmin")>
        Public Async Function HallBookings() As Task(Of IActionResult)
            Dim bookings = Await _hallBookingService.GetAllBookingsAsync()
            Return View(bookings)
        End Function

        <Authorize(Roles:="SuperAdmin")>
        Public Function Calendar() As IActionResult
            Return View()
        End Function

        <Authorize(Roles:="SuperAdmin")>
        <HttpGet>
        Public Async Function GetCalendarEvents() As Task(Of JsonResult)
            Dim events = Await _hallBookingService.GetCalendarEventsAsync()
            Return Json(events)
        End Function
    End Class

    <Authorize(Roles:="SuperAdmin")>
    Public Class FacilityController
        Inherits Controller

        Private ReadOnly _hallBookingService As IHallBookingService

        Public Sub New(hallBookingService As IHallBookingService)
            _hallBookingService = hallBookingService
        End Sub

        Public Async Function Index() As Task(Of IActionResult)
            Dim facilities = Await _hallBookingService.GetAllFacilitiesAsync()
            Return View(facilities)
        End Function

        <HttpGet>
        Public Function Create() As IActionResult
            Return View(New HallViewModel())
        End Function

        <HttpPost>
        <ValidateAntiForgeryToken>
        Public Async Function Create(vm As HallViewModel) As Task(Of IActionResult)
            If Not ModelState.IsValid Then Return View(vm)
            Await _hallBookingService.CreateFacilityAsync(vm)
            TempData("Success") = $"Facility '{vm.Name}' created successfully."
            Return RedirectToAction("Index")
        End Function

        <HttpGet>
        Public Async Function Edit(id As Integer) As Task(Of IActionResult)
            Dim facility = Await _hallBookingService.GetFacilityByIdAsync(id)
            If facility Is Nothing Then Return NotFound()
            Dim vm = New HallViewModel With {
                .Id = facility.Id,
                .Name = facility.Name,
                .FacilityType = facility.FacilityType,
                .Description = facility.Description,
                .Location = facility.Location,
                .Capacity = facility.Capacity,
                .Facilities = facility.Facilities,
                .RentalRatePerDay = facility.RentalRatePerDay,
                .DisplayOrder = facility.DisplayOrder,
                .Status = facility.Status,
                .IsActive = facility.IsActive,
                .ImagePath = facility.ImagePath
            }
            Return View(vm)
        End Function

        <HttpPost>
        <ValidateAntiForgeryToken>
        Public Async Function Edit(vm As HallViewModel) As Task(Of IActionResult)
            If Not ModelState.IsValid Then Return View(vm)
            Dim success = Await _hallBookingService.UpdateFacilityAsync(vm)
            If success Then
                TempData("Success") = $"Facility '{vm.Name}' updated successfully."
            Else
                TempData("Error") = "Facility not found."
            End If
            Return RedirectToAction("Index")
        End Function

        <HttpPost>
        <ValidateAntiForgeryToken>
        Public Async Function ToggleActive(id As Integer) As Task(Of IActionResult)
            Dim facility = Await _hallBookingService.GetFacilityByIdAsync(id)
            If facility IsNot Nothing Then
                Dim vm = New HallViewModel With {
                    .Id = facility.Id,
                    .Name = facility.Name,
                    .FacilityType = facility.FacilityType,
                    .Description = facility.Description,
                    .Location = facility.Location,
                    .Capacity = facility.Capacity,
                    .Facilities = facility.Facilities,
                    .RentalRatePerDay = facility.RentalRatePerDay,
                    .DisplayOrder = facility.DisplayOrder,
                    .Status = facility.Status,
                    .IsActive = Not facility.IsActive,
                    .ImagePath = facility.ImagePath
                }
                Await _hallBookingService.UpdateFacilityAsync(vm)
                TempData("Success") = $"Facility '{facility.Name}' {If(Not facility.IsActive, "activated", "deactivated")}."
            End If
            Return RedirectToAction("Index")
        End Function
    End Class
End Namespace
