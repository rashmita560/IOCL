Imports Microsoft.AspNetCore.Mvc
Imports Microsoft.AspNetCore.Mvc.Filters
Imports Microsoft.AspNetCore.Identity
Imports IOCLCommunityHall.Models.Entities
Imports IOCLCommunityHall.Services

Namespace Controllers
    ''' <summary>
    ''' Global action filter applied to every MVC controller action.
    '''
    ''' Responsibilities:
    '''   1. Force-redirect users who must change their password to the ChangePassword page.
    '''   2. Inject unread notification count + recent notifications into ViewBag for the nav bar.
    '''   3. Trigger the inventory release engine on every authenticated page load so that
    '''      expired reservations are guaranteed to be released when any page is visited
    '''      (Dashboard, Inventory, Approvals, My Rentals, AdminRequest, etc.).
    ''' </summary>
    Public Class NotificationActionFilter
        Implements IAsyncActionFilter

        Private ReadOnly _userManager As UserManager(Of ApplicationUser)
        Private ReadOnly _notificationService As INotificationService
        Private ReadOnly _releaseEngine As IInventoryReleaseEngine

        Public Sub New(userManager As UserManager(Of ApplicationUser),
                       notificationService As INotificationService,
                       releaseEngine As IInventoryReleaseEngine)
            _userManager = userManager
            _notificationService = notificationService
            _releaseEngine = releaseEngine
        End Sub

        Public Async Function OnActionExecutionAsync(context As ActionExecutingContext, [next] As ActionExecutionDelegate) As Task Implements IAsyncActionFilter.OnActionExecutionAsync
            ' ── 1. Force password change if required ───────────────────────────────
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

            ' ── 2. Trigger inventory release engine (fire-and-forget style) ────────
            ' Safe to call on every page load — the engine is a no-op if nothing is due.
            ' Only run for authenticated users (avoids overhead on public pages).
            If context.HttpContext.User.Identity?.IsAuthenticated = True Then
                Try
                    Await _releaseEngine.TriggerReleaseAsync()
                Catch ex As Exception
                    ' Release errors must never crash a user-facing page
                    ' Errors are already logged inside the engine
                End Try
            End If

            ' ── 3. Execute the action ───────────────────────────────────────────────
            Dim resultContext = Await [next]()

            ' ── 4. Inject notification data into ViewBag ───────────────────────────
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
