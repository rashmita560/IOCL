Imports Microsoft.AspNetCore.Mvc
Imports Microsoft.AspNetCore.Authorization
Imports Microsoft.AspNetCore.Identity
Imports Microsoft.EntityFrameworkCore
Imports IOCLCommunityHall.Data
Imports IOCLCommunityHall.Models.Entities
Imports IOCLCommunityHall.Models.ViewModels
Imports IOCLCommunityHall.Services

Namespace Controllers
    <Authorize>
    Public Class UserDashboardController
        Inherits Controller

        Private ReadOnly _inventoryService As IInventoryService
        Private ReadOnly _hallBookingService As IHallBookingService
        Private ReadOnly _rentalService As IRentalService
        Private ReadOnly _notificationService As INotificationService
        Private ReadOnly _userManager As UserManager(Of ApplicationUser)

        Public Sub New(inventoryService As IInventoryService,
                       hallBookingService As IHallBookingService,
                       rentalService As IRentalService,
                       notificationService As INotificationService,
                       userManager As UserManager(Of ApplicationUser))
            _inventoryService = inventoryService
            _hallBookingService = hallBookingService
            _rentalService = rentalService
            _notificationService = notificationService
            _userManager = userManager
        End Sub

        Public Async Function Index() As Task(Of IActionResult)
            Dim currentUser As ApplicationUser = Await _userManager.GetUserAsync(Me.User)
            Dim vm = New DashboardViewModel With {
                .AvailableHalls = Nothing,
                .AvailableInventory = (Await _inventoryService.GetInventoryForRequestAsync()).ToList(),
                .UserRequests = (Await _rentalService.GetUserRequestsAsync(currentUser.Id)).Take(5).ToList()
            }
            Return View(vm)
        End Function
    End Class

    <Authorize>
    Public Class RentalRequestController
        Inherits Controller

        Private ReadOnly _rentalService As IRentalService
        Private ReadOnly _inventoryService As IInventoryService
        Private ReadOnly _userManager As UserManager(Of ApplicationUser)

        Public Sub New(rentalService As IRentalService,
                       inventoryService As IInventoryService,
                       userManager As UserManager(Of ApplicationUser))
            _rentalService = rentalService
            _inventoryService = inventoryService
            _userManager = userManager
        End Sub

        <HttpGet>
        Public Async Function Create() As Task(Of IActionResult)
            Dim vm = New RentalRequestViewModel With {
                .AvailableItems = (Await _inventoryService.GetInventoryForRequestAsync()).ToList(),
                .EventDate = DateTime.Today.AddDays(1),
                .StartDate = DateTime.Today.AddDays(1),
                .EndDate = DateTime.Today.AddDays(2)
            }
            Return View(vm)
        End Function

        <HttpPost>
        <ValidateAntiForgeryToken>
        Public Async Function Create(vm As RentalRequestViewModel) As Task(Of IActionResult)
            If Not ModelState.IsValid Then
                vm.AvailableItems = (Await _inventoryService.GetInventoryForRequestAsync()).ToList()
                Return View(vm)
            End If

            Dim currentUser As ApplicationUser = Await _userManager.GetUserAsync(Me.User)

            ' Detect submitter's primary role for routing
            Dim roles = Await _userManager.GetRolesAsync(currentUser)
            Dim submitterRole = "User"
            For Each r In New String() {"SuperAdmin", "GM", "HOD", "User"}
                If roles.Contains(r) Then
                    submitterRole = r
                    Exit For
                End If
            Next

            Dim request = Await _rentalService.CreateRequestAsync(vm, currentUser.Id, submitterRole)
            TempData("Success") = $"Request #{request.RequestNumber} submitted successfully! Status: Pending approval."
            Return RedirectToAction("MyRequests")
        End Function

        Public Async Function MyRequests() As Task(Of IActionResult)
            Dim currentUser As ApplicationUser = Await _userManager.GetUserAsync(Me.User)
            Dim requests = Await _rentalService.GetUserRequestsAsync(currentUser.Id)
            Return View(requests)
        End Function

        ' AJAX endpoint: get item price for live calculation
        <HttpGet>
        Public Async Function GetItemPrice(itemId As Integer) As Task(Of JsonResult)
            Dim items = Await _inventoryService.GetAllItemsAsync()
            Dim item = items.FirstOrDefault(Function(i) i.Id = itemId)
            If item Is Nothing Then Return Json(New With {.success = False})
            Return Json(New With {
                .success = True,
                .price = item.CurrentPrice,
                .available = item.AvailableQuantity,
                .unit = item.UnitType
            })
        End Function
    End Class

    <Authorize>
    Public Class HallBookingController
        Inherits Controller

        Private ReadOnly _hallBookingService As IHallBookingService
        Private ReadOnly _userManager As UserManager(Of ApplicationUser)

        Public Sub New(hallBookingService As IHallBookingService, userManager As UserManager(Of ApplicationUser))
            _hallBookingService = hallBookingService
            _userManager = userManager
        End Sub

        Public Async Function Index() As Task(Of IActionResult)
            Dim facilities = Await _hallBookingService.GetAllFacilitiesAsync()
            Dim currentUser As ApplicationUser = Await _userManager.GetUserAsync(Me.User)
            Dim myBookings = Await _hallBookingService.GetUserBookingsAsync(currentUser.Id)
            ViewBag.MyBookings = myBookings
            Return View(facilities)
        End Function

        <HttpGet>
        Public Async Function Create(facilityId As Integer) As Task(Of IActionResult)
            Dim vm = New HallBookingViewModel With {
                .SelectedFacilityIds = If(facilityId > 0, New List(Of Integer) From {facilityId}, New List(Of Integer)()),
                .AvailableFacilities = (Await _hallBookingService.GetAllFacilitiesAsync()).ToList(),
                .EventDate = DateTime.Today.AddDays(1)
            }
            Return View(vm)
        End Function

        <HttpPost>
        <ValidateAntiForgeryToken>
        Public Async Function Create(vm As HallBookingViewModel) As Task(Of IActionResult)
            If vm.SelectedFacilityIds Is Nothing OrElse vm.SelectedFacilityIds.Count = 0 Then
                ModelState.AddModelError("SelectedFacilityIds", "Please select at least one facility.")
            End If
            If Not ModelState.IsValid Then
                vm.AvailableFacilities = (Await _hallBookingService.GetAllFacilitiesAsync()).ToList()
                Return View(vm)
            End If
            Dim currentUser As ApplicationUser = Await _userManager.GetUserAsync(Me.User)
            Dim result = Await _hallBookingService.CreateBookingAsync(vm, currentUser.Id)
            If result.Success Then
                TempData("Success") = result.Message
                Return RedirectToAction("Index")
            End If
            ModelState.AddModelError("", result.Message)
            vm.AvailableFacilities = (Await _hallBookingService.GetAllFacilitiesAsync()).ToList()
            Return View(vm)
        End Function

        ' AJAX: check facility availability (accepts comma-separated facilityIds)
        <HttpGet>
        Public Async Function CheckAvailability(facilityIds As String, [date] As String, startTime As String, endTime As String) As Task(Of JsonResult)
            Dim eventDate As DateTime
            Dim sTime As TimeSpan
            Dim eTime As TimeSpan
            If Not DateTime.TryParse([date], eventDate) Then Return Json(New With {.available = False, .message = "Invalid date"})
            If Not TimeSpan.TryParse(startTime, sTime) Then Return Json(New With {.available = False, .message = "Invalid start time"})
            If Not TimeSpan.TryParse(endTime, eTime) Then Return Json(New With {.available = False, .message = "Invalid end time"})
            Dim ids = facilityIds.Split(","c, StringSplitOptions.RemoveEmptyEntries).
                Select(Function(s) Integer.TryParse(s.Trim(), Nothing) AndAlso True)  ' parse safely
            Dim idList = facilityIds.Split(","c, StringSplitOptions.RemoveEmptyEntries).
                Select(Function(s) CInt(s.Trim())).ToList()
            If idList.Count = 0 Then Return Json(New With {.available = False, .message = "No facilities selected"})
            Dim result = Await _hallBookingService.AreFacilitiesAvailableAsync(idList, eventDate, sTime, eTime)
            Return Json(New With {.available = result.Available, .conflict = result.ConflictingFacility})
        End Function
    End Class

    <Authorize>
    Public Class NotificationController
        Inherits Controller

        Private ReadOnly _notificationService As INotificationService
        Private ReadOnly _userManager As UserManager(Of ApplicationUser)

        Public Sub New(notificationService As INotificationService, userManager As UserManager(Of ApplicationUser))
            _notificationService = notificationService
            _userManager = userManager
        End Sub

        Public Async Function Index() As Task(Of IActionResult)
            Dim currentUser As ApplicationUser = Await _userManager.GetUserAsync(Me.User)
            Dim notifs = Await _notificationService.GetUserNotificationsAsync(currentUser.Id)
            Return View(notifs)
        End Function

        <HttpPost>
        Public Async Function MarkRead(id As Integer) As Task(Of IActionResult)
            Await _notificationService.MarkAsReadAsync(id)
            Return Ok()
        End Function

        Public Async Function MarkAllRead() As Task(Of IActionResult)
            Dim currentUser As ApplicationUser = Await _userManager.GetUserAsync(Me.User)
            Await _notificationService.MarkAllAsReadAsync(currentUser.Id)
            Return RedirectToAction("Index")
        End Function

        <HttpGet>
        Public Async Function GetUnreadCount() As Task(Of JsonResult)
            Dim currentUser As ApplicationUser = Await _userManager.GetUserAsync(Me.User)
            Dim count = Await _notificationService.GetUnreadCountAsync(currentUser.Id)
            Return Json(New With {.count = count})
        End Function
    End Class
End Namespace
