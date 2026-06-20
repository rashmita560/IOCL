Imports Microsoft.AspNetCore.Mvc
Imports Microsoft.AspNetCore.Authorization
Imports Microsoft.AspNetCore.Identity
Imports IOCLCommunityHall.Models.Entities
Imports IOCLCommunityHall.Services

Namespace Controllers
    Public Class HomeController
        Inherits Controller

        Public Function Index() As IActionResult
            If User.Identity?.IsAuthenticated Then
                If User.IsInRole("Admin") OrElse User.IsInRole("SuperAdmin") Then
                    Return RedirectToAction("Index", "AdminDashboard")
                End If
                Return RedirectToAction("Index", "UserDashboard")
            End If
            Return RedirectToAction("Login", "Account")
        End Function

        Public Function [Error]() As IActionResult
            Dim exceptionHandlerPathFeature = HttpContext.Features.Get(Of Microsoft.AspNetCore.Diagnostics.IExceptionHandlerPathFeature)()
            Dim exceptionMessage As String = ""
            Dim stackTrace As String = ""
            If exceptionHandlerPathFeature IsNot Nothing Then
                Dim err = exceptionHandlerPathFeature.Error
                exceptionMessage = err.Message
                stackTrace = err.StackTrace
            End If
            ViewData("ExceptionMessage") = exceptionMessage
            ViewData("StackTrace") = stackTrace
            Return View()
        End Function
    End Class

    Public Class AccountController
        Inherits Controller

        Private ReadOnly _userManager As UserManager(Of ApplicationUser)
        Private ReadOnly _signInManager As SignInManager(Of ApplicationUser)

        Public Sub New(userManager As UserManager(Of ApplicationUser), signInManager As SignInManager(Of ApplicationUser))
            _userManager = userManager
            _signInManager = signInManager
        End Sub

        <HttpGet>
        Public Function Login(returnUrl As String) As IActionResult
            If User.Identity?.IsAuthenticated Then Return RedirectToAction("Index", "Home")
            ViewData("ReturnUrl") = returnUrl
            Return View()
        End Function

        <HttpPost>
        <ValidateAntiForgeryToken>
        Public Async Function Login(employeeId As String, password As String, rememberMe As Boolean, returnUrl As String) As Task(Of IActionResult)
            If String.IsNullOrEmpty(employeeId) OrElse String.IsNullOrEmpty(password) Then
                ModelState.AddModelError("", "Employee ID and password are required.")
                Return View()
            End If

            Dim user = Await _userManager.FindByNameAsync(employeeId)
            If user Is Nothing OrElse Not user.IsActive Then
                ModelState.AddModelError("", "Invalid credentials or account is inactive.")
                Return View()
            End If

            Dim result = Await _signInManager.PasswordSignInAsync(user, password, rememberMe, lockoutOnFailure:=True)
            If result.Succeeded Then
                user.LastLoginAt = DateTime.UtcNow
                Await _userManager.UpdateAsync(user)

                ' Role-based redirect – never send an Employee to an admin-only page
                Dim roles = Await _userManager.GetRolesAsync(user)
                Dim isAdmin = roles.Contains("Admin") OrElse roles.Contains("SuperAdmin")

                If isAdmin Then
                    ' Admins can follow a valid ReturnUrl (e.g. deep-link back to their page)
                    If Not String.IsNullOrEmpty(returnUrl) AndAlso Url.IsLocalUrl(returnUrl) Then
                        Return Redirect(returnUrl)
                    End If
                    Return RedirectToAction("Index", "AdminDashboard")
                Else
                    ' Employees always land on the user dashboard regardless of ReturnUrl
                    Return RedirectToAction("Index", "UserDashboard")
                End If
            End If

            If result.IsLockedOut Then
                ModelState.AddModelError("", "Account is locked out. Try after 15 minutes.")
            Else
                ModelState.AddModelError("", "Invalid Employee ID or password.")
            End If
            Return View()
        End Function

        <HttpPost>
        <ValidateAntiForgeryToken>
        Public Async Function Logout() As Task(Of IActionResult)
            Await _signInManager.SignOutAsync()
            Return RedirectToAction("Login")
        End Function

        Public Function AccessDenied() As IActionResult
            Return View()
        End Function

        <HttpGet>
        Public Function ChangePassword() As IActionResult
            If Not User.Identity?.IsAuthenticated = True Then
                Return RedirectToAction("Login")
            End If
            Return View()
        End Function

        <HttpPost>
        <ValidateAntiForgeryToken>
        Public Async Function ChangePassword(currentPassword As String, newPassword As String, confirmPassword As String) As Task(Of IActionResult)
            If Not User.Identity?.IsAuthenticated = True Then
                Return RedirectToAction("Login")
            End If

            If String.IsNullOrEmpty(currentPassword) OrElse String.IsNullOrEmpty(newPassword) Then
                ModelState.AddModelError("", "All fields are required.")
                Return View()
            End If

            If newPassword <> confirmPassword Then
                ModelState.AddModelError("", "The new password and confirmation password do not match.")
                Return View()
            End If

            Dim appUser = Await _userManager.GetUserAsync(Me.User)
            If appUser Is Nothing Then
                Return RedirectToAction("Login")
            End If

            Dim result = Await _userManager.ChangePasswordAsync(appUser, currentPassword, newPassword)
            If result.Succeeded Then
                appUser.MustChangePassword = False
                Await _userManager.UpdateAsync(appUser)
                Await _signInManager.RefreshSignInAsync(appUser)
                TempData("Success") = "Your password has been changed successfully."

                Dim roles = Await _userManager.GetRolesAsync(appUser)
                Dim isAdmin = roles.Contains("Admin") OrElse roles.Contains("SuperAdmin")
                If isAdmin Then
                    Return RedirectToAction("Index", "AdminDashboard")
                Else
                    Return RedirectToAction("Index", "UserDashboard")
                End If
            End If

            For Each [error] In result.Errors
                ModelState.AddModelError("", [error].Description)
            Next
            Return View()
        End Function
    End Class
End Namespace