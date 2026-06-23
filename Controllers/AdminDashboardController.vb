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
        Private ReadOnly _userManager As UserManager(Of ApplicationUser)
        Private ReadOnly _auditService As IAuditService
        Private ReadOnly _notificationService As INotificationService

        Public Sub New(rentalService As IRentalService,
                       userManager As UserManager(Of ApplicationUser),
                       auditService As IAuditService,
                       notificationService As INotificationService)
            _rentalService = rentalService
            _userManager = userManager
            _auditService = auditService
            _notificationService = notificationService
        End Sub

        Public Async Function Index(statusFilter As String, search As String) As Task(Of IActionResult)
            Dim currentUser As ApplicationUser = Await _userManager.GetUserAsync(Me.User)
            Dim roles = Await _userManager.GetRolesAsync(currentUser)
            Dim currentRole = If(roles.FirstOrDefault(), "User")

            ' Default statusFilter to "Pending" on load
            If String.IsNullOrEmpty(statusFilter) Then
                statusFilter = "Pending"
            End If

            Dim allRequests = Await _rentalService.GetAllRequestsAsync()

            ' 1. Calculate Priority Ranks for Pending requests at this role's stage (excluding self requests)
            Dim basePendingRequests = allRequests.Where(Function(r) r.Status = RequestStatus.Pending AndAlso r.UserId <> currentUser.Id)
            If currentRole = "HOD" Then
                basePendingRequests = basePendingRequests.Where(Function(r) r.ApprovalStage = ApprovalStage.PendingHOD)
            ElseIf currentRole = "GM" Then
                basePendingRequests = basePendingRequests.Where(Function(r) r.ApprovalStage = ApprovalStage.PendingGM)
            ElseIf currentRole = "SuperAdmin" Then
                basePendingRequests = basePendingRequests.Where(Function(r) r.ApprovalStage = ApprovalStage.PendingHR)
            End If

            Dim orderedBase = basePendingRequests.OrderBy(Function(r) r.CreatedAt).ToList()
            Dim rankDict = New System.Collections.Generic.Dictionary(Of Integer, Integer)()
            For i = 0 To orderedBase.Count - 1
                rankDict(orderedBase(i).Id) = i + 1
            Next
            ViewBag.RankDict = rankDict

            ' 2. Filter requests to show in the table
            Dim requests As IEnumerable(Of RentalRequest)

            If currentRole = "HOD" Then
                If statusFilter = "Pending" Then
                    requests = orderedBase
                ElseIf statusFilter = "All" Then
                    requests = allRequests
                Else
                    Dim status As RequestStatus
                    If [Enum].TryParse(statusFilter, status) Then
                        requests = allRequests.Where(Function(r) r.Status = status)
                    Else
                        requests = allRequests
                    End If
                End If

            ElseIf currentRole = "GM" Then
                If statusFilter = "Pending" Then
                    requests = orderedBase
                ElseIf statusFilter = "All" Then
                    ' Show all requests that have gone through or are at the GM stage
                    requests = allRequests.Where(Function(r) r.ApprovalStage = ApprovalStage.PendingGM OrElse r.GMApprovedAt IsNot Nothing)
                Else
                    Dim status As RequestStatus
                    If [Enum].TryParse(statusFilter, status) Then
                        requests = allRequests.Where(Function(r) r.Status = status AndAlso
                                                                (r.ApprovalStage = ApprovalStage.PendingGM OrElse r.GMApprovedAt IsNot Nothing))
                    Else
                        requests = allRequests.Where(Function(r) r.ApprovalStage = ApprovalStage.PendingGM OrElse r.GMApprovedAt IsNot Nothing)
                    End If
                End If

            ElseIf currentRole = "SuperAdmin" Then
                If statusFilter = "Pending" Then
                    requests = orderedBase
                ElseIf statusFilter = "All" Then
                    requests = allRequests
                Else
                    Dim status As RequestStatus
                    If [Enum].TryParse(statusFilter, status) Then
                        requests = allRequests.Where(Function(r) r.Status = status)
                    Else
                        requests = allRequests
                    End If
                End If
            Else
                requests = allRequests
            End If

            ' Apply search filter
            If Not String.IsNullOrEmpty(search) Then
                requests = requests.Where(Function(r) r.RequestNumber.Contains(search, StringComparison.OrdinalIgnoreCase) OrElse
                                                       r.User?.FullName.Contains(search, StringComparison.OrdinalIgnoreCase))
            End If

            ViewBag.StatusFilter = statusFilter
            ViewBag.Search = search
            ViewBag.CurrentRole = currentRole
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
            Dim success = Await _rentalService.RejectRequestAsync(id, reason, currentUser.EmployeeId)
            Await _auditService.LogAsync(currentUser.Id, "Reject", "RentalRequest", id.ToString(),
                $"Request rejected. Reason: {reason}", "", "Rejected",
                HttpContext.Connection.RemoteIpAddress?.ToString())
            TempData(If(success, "Success", "Error")) = If(success, "Request rejected.", "Could not reject request.")
            Return RedirectToAction("Details", New With {.id = id})
        End Function

    End Class
End Namespace
