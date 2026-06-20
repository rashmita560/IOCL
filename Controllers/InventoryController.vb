Imports Microsoft.AspNetCore.Mvc
Imports Microsoft.AspNetCore.Authorization
Imports Microsoft.AspNetCore.Identity
Imports Microsoft.EntityFrameworkCore
Imports IOCLCommunityHall.Data
Imports IOCLCommunityHall.Models.Entities
Imports IOCLCommunityHall.Models.ViewModels
Imports IOCLCommunityHall.Services

Namespace Controllers
    <Authorize(Roles:="SuperAdmin")>
    Public Class InventoryController
        Inherits Controller

        Private ReadOnly _inventoryService As IInventoryService
        Private ReadOnly _auditService As IAuditService
        Private ReadOnly _userManager As UserManager(Of ApplicationUser)
        Private ReadOnly _notificationService As INotificationService

        Public Sub New(inventoryService As IInventoryService,
                       auditService As IAuditService,
                       userManager As UserManager(Of ApplicationUser),
                       notificationService As INotificationService)
            _inventoryService = inventoryService
            _auditService = auditService
            _userManager = userManager
            _notificationService = notificationService
        End Sub

        Private Async Function SetNotifViewBag() As Task
            Dim currentUser As ApplicationUser = Await _userManager.GetUserAsync(Me.User)
            If currentUser IsNot Nothing Then
                ViewBag.UnreadNotifications = Await _notificationService.GetUnreadCountAsync(currentUser.Id)
                ViewBag.RecentNotifications = (Await _notificationService.GetUserNotificationsAsync(currentUser.Id)).Take(5).ToList()
            Else
                ViewBag.UnreadNotifications = 0
                ViewBag.RecentNotifications = New List(Of IOCLCommunityHall.Models.Entities.Notification)()
            End If
        End Function

        Public Async Function Index(search As String, categoryId As Integer?) As Task(Of IActionResult)
            Dim items = (Await _inventoryService.GetAllItemsAsync()).ToList()
            If Not String.IsNullOrEmpty(search) Then
                items = items.Where(Function(i) i.Name.Contains(search, StringComparison.OrdinalIgnoreCase)).ToList()
            End If
            If categoryId.HasValue Then
                items = items.Where(Function(i) i.CategoryId = categoryId.Value).ToList()
            End If
            ViewBag.Categories = Await _inventoryService.GetCategoriesAsync()
            ViewBag.Search = search
            ViewBag.CategoryFilter = categoryId
            Await SetNotifViewBag()
            Return View(items)
        End Function

        <HttpGet>
        Public Async Function Create() As Task(Of IActionResult)
            Dim vm = New InventoryViewModel With {
                .Categories = (Await _inventoryService.GetCategoriesAsync()).ToList(),
                .IsActive = True,
                .UnitType = "Nos"
            }
            Await SetNotifViewBag()
            Return View(vm)
        End Function

        <HttpPost>
        <ValidateAntiForgeryToken>
        Public Async Function Create(vm As InventoryViewModel) As Task(Of IActionResult)
            If Not ModelState.IsValid Then
                vm.Categories = (Await _inventoryService.GetCategoriesAsync()).ToList()
                Await SetNotifViewBag()
                Return View(vm)
            End If
            Await _inventoryService.CreateItemAsync(vm)
            Dim currentUser As ApplicationUser = Await _userManager.GetUserAsync(Me.User)
            Await _auditService.LogAsync(currentUser.Id, "Create", "InventoryItem", "0", $"New item '{vm.Name}' created.", "", $"Price: ₹{vm.CurrentPrice}", HttpContext.Connection.RemoteIpAddress?.ToString())
            TempData("Success") = $"Item '{vm.Name}' added successfully."
            Return RedirectToAction("Index")
        End Function

        <HttpGet>
        Public Async Function Edit(id As Integer) As Task(Of IActionResult)
            Dim item = Await _inventoryService.GetItemByIdAsync(id)
            If item Is Nothing Then Return NotFound()
            Dim vm = New InventoryViewModel With {
                .Id = item.Id,
                .Name = item.Name,
                .Description = item.Description,
                .CategoryId = item.CategoryId,
                .UnitType = item.UnitType,
                .TotalQuantity = item.TotalQuantity,
                .ReservedQuantity = item.ReservedQuantity,
                .CurrentPrice = item.CurrentPrice,
                .IsActive = item.IsActive,
                .ImagePath = item.ImagePath,
                .Categories = (Await _inventoryService.GetCategoriesAsync()).ToList()
            }
            Await SetNotifViewBag()
            Return View(vm)
        End Function

        <HttpPost>
        <ValidateAntiForgeryToken>
        Public Async Function Edit(vm As InventoryViewModel) As Task(Of IActionResult)
            If Not ModelState.IsValid Then
                vm.Categories = (Await _inventoryService.GetCategoriesAsync()).ToList()
                Await SetNotifViewBag()
                Return View(vm)
            End If
            Dim currentUser As ApplicationUser = Await _userManager.GetUserAsync(Me.User)
            Await _inventoryService.UpdateItemAsync(vm, currentUser.UserName)
            Await _auditService.LogAsync(currentUser.Id, "Update", "InventoryItem", vm.Id.ToString(), $"Item '{vm.Name}' updated.", "", $"Price: ₹{vm.CurrentPrice}", HttpContext.Connection.RemoteIpAddress?.ToString())
            TempData("Success") = $"Item '{vm.Name}' updated successfully."
            Return RedirectToAction("Index")
        End Function

        <HttpPost>
        <ValidateAntiForgeryToken>
        Public Async Function Delete(id As Integer) As Task(Of IActionResult)
            Await _inventoryService.DeleteItemAsync(id)
            Dim currentUser As ApplicationUser = Await _userManager.GetUserAsync(Me.User)
            Await _auditService.LogAsync(currentUser.Id, "Delete", "InventoryItem", id.ToString(), "Item deactivated (soft delete)", "", "", HttpContext.Connection.RemoteIpAddress?.ToString())
            TempData("Success") = "Item removed from inventory."
            Return RedirectToAction("Index")
        End Function
    End Class

    <Authorize(Roles:="SuperAdmin")>
    Public Class PriceController
        Inherits Controller

        Private ReadOnly _inventoryService As IInventoryService
        Private ReadOnly _auditService As IAuditService
        Private ReadOnly _userManager As UserManager(Of ApplicationUser)
        Private ReadOnly _notificationService As INotificationService

        Public Sub New(inventoryService As IInventoryService,
                       auditService As IAuditService,
                       userManager As UserManager(Of ApplicationUser),
                       notificationService As INotificationService)
            _inventoryService = inventoryService
            _auditService = auditService
            _userManager = userManager
            _notificationService = notificationService
        End Sub

        Private Async Function SetNotifViewBag() As Task
            Dim currentUser As ApplicationUser = Await _userManager.GetUserAsync(Me.User)
            If currentUser IsNot Nothing Then
                ViewBag.UnreadNotifications = Await _notificationService.GetUnreadCountAsync(currentUser.Id)
                ViewBag.RecentNotifications = (Await _notificationService.GetUserNotificationsAsync(currentUser.Id)).Take(5).ToList()
            Else
                ViewBag.UnreadNotifications = 0
                ViewBag.RecentNotifications = New List(Of IOCLCommunityHall.Models.Entities.Notification)()
            End If
        End Function

        Public Async Function Index() As Task(Of IActionResult)
            Dim vm = New PriceViewModel With {
                .AllItems = (Await _inventoryService.GetAllItemsAsync()).ToList()
            }
            Await SetNotifViewBag()
            Return View(vm)
        End Function

        <HttpPost>
        <ValidateAntiForgeryToken>
        Public Async Function UpdatePrice(vm As PriceViewModel) As Task(Of IActionResult)
            If Not ModelState.IsValid Then
                vm.AllItems = (Await _inventoryService.GetAllItemsAsync()).ToList()
                Await SetNotifViewBag()
                Return View("Index", vm)
            End If
            Dim currentUser As ApplicationUser = Await _userManager.GetUserAsync(Me.User)
            Dim item = Await _inventoryService.GetItemByIdAsync(vm.ItemId)
            Await _inventoryService.UpdatePriceAsync(vm.ItemId, vm.NewPrice, vm.EffectiveDate, vm.Reason, currentUser.UserName)
            Await _auditService.LogAsync(currentUser.Id, "PriceUpdate", "InventoryItem", vm.ItemId.ToString(),
                $"Price updated for {item?.Name}: ₹{vm.CurrentPrice} → ₹{vm.NewPrice}",
                $"₹{vm.CurrentPrice}", $"₹{vm.NewPrice}", HttpContext.Connection.RemoteIpAddress?.ToString())
            TempData("Success") = $"Price updated for '{item?.Name}' to ₹{vm.NewPrice}. History recorded."
            Return RedirectToAction("Index")
        End Function

        Public Async Function History(itemId As Integer?) As Task(Of IActionResult)
            Dim histories As IEnumerable(Of PriceHistory)
            If itemId.HasValue Then
                histories = Await _inventoryService.GetPriceHistoryAsync(itemId.Value)
                Dim item = Await _inventoryService.GetItemByIdAsync(itemId.Value)
                ViewBag.ItemName = item?.Name
            Else
                histories = Await _inventoryService.GetAllPriceHistoriesAsync()
                ViewBag.ItemName = "All Items"
            End If
            ViewBag.AllItems = (Await _inventoryService.GetAllItemsAsync()).ToList()
            ViewBag.SelectedItem = itemId
            Await SetNotifViewBag()
            Return View(histories)
        End Function
    End Class

    <Authorize(Roles:="SuperAdmin")>
    Public Class ReportsController
        Inherits Controller

        Private ReadOnly _reportService As IReportService
        Private ReadOnly _userManager As UserManager(Of ApplicationUser)
        Private ReadOnly _notificationService As INotificationService

        Public Sub New(reportService As IReportService,
                       userManager As UserManager(Of ApplicationUser),
                       notificationService As INotificationService)
            _reportService = reportService
            _userManager = userManager
            _notificationService = notificationService
        End Sub

        Private Async Function SetNotifViewBag() As Task
            Dim currentUser As ApplicationUser = Await _userManager.GetUserAsync(Me.User)
            If currentUser IsNot Nothing Then
                ViewBag.UnreadNotifications = Await _notificationService.GetUnreadCountAsync(currentUser.Id)
                ViewBag.RecentNotifications = (Await _notificationService.GetUserNotificationsAsync(currentUser.Id)).Take(5).ToList()
            Else
                ViewBag.UnreadNotifications = 0
                ViewBag.RecentNotifications = New List(Of IOCLCommunityHall.Models.Entities.Notification)()
            End If
        End Function

        Public Async Function Index() As Task(Of IActionResult)
            Dim vm = New ReportViewModel With {
                .ReportType = ReportType.Monthly,
                .SelectedYear = DateTime.Today.Year,
                .SelectedMonth = DateTime.Today.Month
            }
            Dim result = Await _reportService.GenerateReportAsync(vm)
            Await SetNotifViewBag()
            Return View(result)
        End Function

        <HttpPost>
        Public Async Function Generate(vm As ReportViewModel) As Task(Of IActionResult)
            Dim result = Await _reportService.GenerateReportAsync(vm)
            Await SetNotifViewBag()
            Return View("Index", result)
        End Function

        Public Async Function ExportCsv(reportType As ReportType, startDate As DateTime, endDate As DateTime, selectedYear As Integer, selectedMonth As Integer) As Task(Of IActionResult)
            Dim vm = New ReportViewModel With {
                .ReportType = reportType, .StartDate = startDate, .EndDate = endDate,
                .SelectedYear = selectedYear, .SelectedMonth = selectedMonth
            }
            Dim data = Await _reportService.ExportToCsvAsync(vm)
            Return File(data, "text/csv", $"IOCL_Report_{DateTime.Now:yyyyMMdd}.csv")
        End Function

        Public Async Function ExportExcel(reportType As ReportType, startDate As DateTime, endDate As DateTime, selectedYear As Integer, selectedMonth As Integer) As Task(Of IActionResult)
            Dim vm = New ReportViewModel With {
                .ReportType = reportType, .StartDate = startDate, .EndDate = endDate,
                .SelectedYear = selectedYear, .SelectedMonth = selectedMonth
            }
            Dim data = Await _reportService.ExportToExcelAsync(vm)
            Return File(data, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"IOCL_Report_{DateTime.Now:yyyyMMdd}.xlsx")
        End Function
    End Class

    <Authorize(Roles:="SuperAdmin")>
    Public Class AuditController
        Inherits Controller

        Private ReadOnly _auditService As IAuditService
        Private ReadOnly _userManager As UserManager(Of ApplicationUser)
        Private ReadOnly _notificationService As INotificationService

        Public Sub New(auditService As IAuditService,
                       userManager As UserManager(Of ApplicationUser),
                       notificationService As INotificationService)
            _auditService = auditService
            _userManager = userManager
            _notificationService = notificationService
        End Sub

        Private Async Function SetNotifViewBag() As Task
            Dim currentUser As ApplicationUser = Await _userManager.GetUserAsync(Me.User)
            If currentUser IsNot Nothing Then
                ViewBag.UnreadNotifications = Await _notificationService.GetUnreadCountAsync(currentUser.Id)
                ViewBag.RecentNotifications = (Await _notificationService.GetUserNotificationsAsync(currentUser.Id)).Take(5).ToList()
            Else
                ViewBag.UnreadNotifications = 0
                ViewBag.RecentNotifications = New List(Of IOCLCommunityHall.Models.Entities.Notification)()
            End If
        End Function

        Public Async Function Index(page As Integer, search As String) As Task(Of IActionResult)
            If page <= 0 Then page = 1
            Dim pageSize = 20
            Dim result = Await _auditService.GetLogsAsync(page, pageSize)
            ViewBag.Page = page
            ViewBag.TotalPages = CInt(Math.Ceiling(CDbl(result.Total) / pageSize))
            ViewBag.Total = result.Total
            Await SetNotifViewBag()
            Return View(result.Logs)
        End Function
    End Class

    <Authorize(Roles:="SuperAdmin")>
    Public Class SuperAdminController
        Inherits Controller

        Private ReadOnly _userManager As UserManager(Of ApplicationUser)
        Private ReadOnly _roleManager As RoleManager(Of IdentityRole)
        Private ReadOnly _notificationService As INotificationService
        Private ReadOnly _context As Data.ApplicationDbContext

        Public Sub New(userManager As UserManager(Of ApplicationUser),
                       roleManager As RoleManager(Of IdentityRole),
                       notificationService As INotificationService,
                       context As Data.ApplicationDbContext)
            _userManager = userManager
            _roleManager = roleManager
            _notificationService = notificationService
            _context = context
        End Sub

        Private Async Function SetNotifViewBag() As Task
            Dim currentUser As ApplicationUser = Await _userManager.GetUserAsync(Me.User)
            If currentUser IsNot Nothing Then
                ViewBag.UnreadNotifications = Await _notificationService.GetUnreadCountAsync(currentUser.Id)
                ViewBag.RecentNotifications = (Await _notificationService.GetUserNotificationsAsync(currentUser.Id)).Take(5).ToList()
            Else
                ViewBag.UnreadNotifications = 0
                ViewBag.RecentNotifications = New List(Of IOCLCommunityHall.Models.Entities.Notification)()
            End If
        End Function

        ' ── User List ─────────────────────────────────────────────────────────
        Public Async Function Users() As Task(Of IActionResult)
            Dim allUsers = _userManager.Users.OrderBy(Function(u) u.EmployeeId).ToList()
            Dim userRoles As New List(Of (User As ApplicationUser, Roles As IList(Of String), Employee As Models.Entities.Employee))
            For Each u In allUsers
                Dim roles = Await _userManager.GetRolesAsync(u)
                Dim emp = Await _context.Employees.FindAsync(u.EmployeeId)
                userRoles.Add((u, roles, emp))
            Next
            Await SetNotifViewBag()
            Return View(userRoles)
        End Function

        ' ── Create User (GET) ─────────────────────────────────────────────────
        <HttpGet>
        Public Async Function CreateUser() As Task(Of IActionResult)
            Await SetNotifViewBag()
            Return View()
        End Function

        ' ── Create User (POST) ───────────────────────────────────────────────
        <HttpPost>
        <ValidateAntiForgeryToken>
        Public Async Function CreateUser(employeeId As String, employeeName As String, department As String,
                                          designation As String, email As String, phoneNumber As String,
                                          quarterAddress As String, password As String, role As String) As Task(Of IActionResult)
            ' Validate Employee ID: exactly 8 digits
            If String.IsNullOrWhiteSpace(employeeId) OrElse employeeId.Length <> 8 OrElse
               Not employeeId.All(Function(c) Char.IsDigit(c)) Then
                TempData("Error") = "Employee ID must be exactly 8 numeric digits."
                Return RedirectToAction("CreateUser")
            End If

            ' Check uniqueness
            If Await _userManager.FindByNameAsync(employeeId) IsNot Nothing Then
                TempData("Error") = $"Employee ID '{employeeId}' is already registered in the system."
                Return RedirectToAction("CreateUser")
            End If

            If Await _context.Employees.FindAsync(employeeId) IsNot Nothing Then
                TempData("Error") = $"Employee record for ID '{employeeId}' already exists."
                Return RedirectToAction("CreateUser")
            End If

            ' Create Employee master record first
            Dim emp = New Models.Entities.Employee With {
                .EmployeeId = employeeId,
                .EmployeeName = If(employeeName, ""),
                .Department = If(department, ""),
                .Designation = If(designation, ""),
                .Email = If(email, ""),
                .PhoneNumber = If(phoneNumber, ""),
                .QuarterAddress = If(quarterAddress, ""),
                .Status = Models.Entities.EmployeeStatus.Active
            }
            _context.Employees.Add(emp)
            Await _context.SaveChangesAsync()

            ' Create Identity auth record
            Dim appUser = New ApplicationUser With {
                .UserName = employeeId,
                .Email = If(email, ""),
                .NormalizedEmail = If(email, "").ToUpperInvariant(),
                .FullName = If(employeeName, ""),
                .EmployeeId = employeeId,
                .Department = If(department, ""),
                .EmailConfirmed = True,
                .IsActive = True,
                .MustChangePassword = True
            }

            Dim result = Await _userManager.CreateAsync(appUser, password)
            If result.Succeeded Then
                Dim assignRole = If(String.IsNullOrEmpty(role), "User", role)
                Await _userManager.AddToRoleAsync(appUser, assignRole)
                TempData("Success") = $"Employee account for '{employeeName}' (ID: {employeeId}) created successfully."
            Else
                ' Rollback employee record if auth creation fails
                _context.Employees.Remove(emp)
                Await _context.SaveChangesAsync()
                TempData("Error") = "Failed to create account: " & String.Join(", ", result.Errors.Select(Function(e) e.Description))
            End If

            Return RedirectToAction("Users")
        End Function

        ' ── Edit User (GET) ───────────────────────────────────────────────────
        <HttpGet>
        Public Async Function EditUser(userId As String) As Task(Of IActionResult)
            Dim user = Await _userManager.FindByIdAsync(userId)
            If user Is Nothing Then Return NotFound()
            Dim emp = Await _context.Employees.FindAsync(user.EmployeeId)
            Dim roles = Await _userManager.GetRolesAsync(user)
            ViewBag.CurrentRole = If(roles.FirstOrDefault(), "User")
            ViewBag.Employee = emp
            Await SetNotifViewBag()
            Return View(user)
        End Function

        ' ── Edit User (POST) ─────────────────────────────────────────────────
        <HttpPost>
        <ValidateAntiForgeryToken>
        Public Async Function EditUser(userId As String, employeeName As String, department As String,
                                        designation As String, email As String, phoneNumber As String,
                                        quarterAddress As String, role As String) As Task(Of IActionResult)
            Dim user = Await _userManager.FindByIdAsync(userId)
            If user Is Nothing Then Return NotFound()

            ' Update auth user mirrors
            user.FullName = If(employeeName, user.FullName)
            user.Department = If(department, user.Department)
            user.Email = If(email, user.Email)
            Await _userManager.UpdateAsync(user)

            ' Update employee master
            Dim emp = Await _context.Employees.FindAsync(user.EmployeeId)
            If emp IsNot Nothing Then
                emp.EmployeeName = If(employeeName, emp.EmployeeName)
                emp.Department = If(department, emp.Department)
                emp.Designation = If(designation, emp.Designation)
                emp.Email = If(email, emp.Email)
                emp.PhoneNumber = If(phoneNumber, emp.PhoneNumber)
                emp.QuarterAddress = If(quarterAddress, emp.QuarterAddress)
                emp.UpdatedDate = DateTime.UtcNow
                Await _context.SaveChangesAsync()
            End If

            ' Update role if changed
            If Not String.IsNullOrEmpty(role) Then
                Dim currentRoles = Await _userManager.GetRolesAsync(user)
                If Not currentRoles.Contains(role) Then
                    Await _userManager.RemoveFromRolesAsync(user, currentRoles)
                    Await _userManager.AddToRoleAsync(user, role)
                End If
            End If

            TempData("Success") = $"Employee '{user.FullName}' updated successfully."
            Return RedirectToAction("Users")
        End Function

        ' ── Toggle Active ─────────────────────────────────────────────────────
        <HttpPost>
        <ValidateAntiForgeryToken>
        Public Async Function ToggleActive(userId As String) As Task(Of IActionResult)
            Dim user = Await _userManager.FindByIdAsync(userId)
            If user IsNot Nothing Then
                user.IsActive = Not user.IsActive
                Await _userManager.UpdateAsync(user)
                TempData("Success") = $"Account {(If(user.IsActive, "activated", "deactivated"))} for {user.FullName}."
            End If
            Return RedirectToAction("Users")
        End Function

        ' ── Change Role ──────────────────────────────────────────────────────
        <HttpPost>
        <ValidateAntiForgeryToken>
        Public Async Function ChangeRole(userId As String, newRole As String) As Task(Of IActionResult)
            Dim user = Await _userManager.FindByIdAsync(userId)
            If user IsNot Nothing Then
                Dim currentRoles = Await _userManager.GetRolesAsync(user)
                Await _userManager.RemoveFromRolesAsync(user, currentRoles)
                Await _userManager.AddToRoleAsync(user, newRole)
                TempData("Success") = $"Role updated to '{newRole}' for {user.FullName}."
            End If
            Return RedirectToAction("Users")
        End Function

        ' ── Reset Password ────────────────────────────────────────────────────
        <HttpPost>
        <ValidateAntiForgeryToken>
        Public Async Function ResetPassword(userId As String, newPassword As String) As Task(Of IActionResult)
            Dim user = Await _userManager.FindByIdAsync(userId)
            If user Is Nothing Then
                TempData("Error") = "User not found."
                Return RedirectToAction("Users")
            End If

            If String.IsNullOrWhiteSpace(newPassword) OrElse newPassword.Length < 6 Then
                TempData("Error") = "Password must be at least 6 characters."
                Return RedirectToAction("Users")
            End If

            Dim token = Await _userManager.GeneratePasswordResetTokenAsync(user)
            Dim result = Await _userManager.ResetPasswordAsync(user, token, newPassword)
            If result.Succeeded Then
                user.MustChangePassword = True
                Await _userManager.UpdateAsync(user)
                TempData("Success") = $"Password reset successfully for Employee ID {user.EmployeeId}. User must change password on next login."
            Else
                TempData("Error") = "Reset failed: " & String.Join(", ", result.Errors.Select(Function(e) e.Description))
            End If
            Return RedirectToAction("Users")
        End Function
    End Class
End Namespace
