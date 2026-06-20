Imports Microsoft.AspNetCore.Mvc
Imports Microsoft.AspNetCore.Mvc.Filters
Imports Microsoft.AspNetCore.Identity
Imports IOCLCommunityHall.Models.Entities
Imports IOCLCommunityHall.Services

Namespace Controllers
    Public Class NotificationActionFilter
        Implements IAsyncActionFilter

        Private ReadOnly _userManager As UserManager(Of ApplicationUser)
        Private ReadOnly _notificationService As INotificationService

        Public Sub New(userManager As UserManager(Of ApplicationUser),
                       notificationService As INotificationService)
            _userManager = userManager
            _notificationService = notificationService
        End Sub

        Public Async Function OnActionExecutionAsync(context As ActionExecutingContext, [next] As ActionExecutionDelegate) As Task Implements IAsyncActionFilter.OnActionExecutionAsync
            If context.HttpContext.User.Identity?.IsAuthenticated = True Then
                Dim currentUser = Await _userManager.GetUserAsync(context.HttpContext.User)
                If currentUser IsNot Nothing AndAlso currentUser.MustChangePassword Then
                    Dim currentController = context.RouteData.Values("controller")?.ToString()
                    Dim currentAction = context.RouteData.Values("action")?.ToString()

                    ' Allow only ChangePassword and Logout actions in AccountController
                    If Not (currentController.Equals("Account", StringComparison.OrdinalIgnoreCase) AndAlso
                            (currentAction.Equals("ChangePassword", StringComparison.OrdinalIgnoreCase) OrElse
                             currentAction.Equals("Logout", StringComparison.OrdinalIgnoreCase))) Then
                        
                        context.Result = New RedirectToActionResult("ChangePassword", "Account", Nothing)
                        Return
                    End If
                End If
            End If

            Dim resultContext = Await [next]()

            Dim controller = TryCast(context.Controller, Controller)
            If controller IsNot Nothing AndAlso TypeOf resultContext.Result Is ViewResult Then
                If context.HttpContext.User.Identity?.IsAuthenticated = True Then
                    Dim currentUser = Await _userManager.GetUserAsync(context.HttpContext.User)
                    If currentUser IsNot Nothing Then
                        controller.ViewBag.UnreadNotifications = Await _notificationService.GetUnreadCountAsync(currentUser.Id)
                        Dim notifs = Await _notificationService.GetUserNotificationsAsync(currentUser.Id)
                        controller.ViewBag.RecentNotifications = notifs.Take(5).ToList()
                    End If
                End If
            End If
        End Function
    End Class
End Namespace
