Imports System.Threading
Imports Microsoft.EntityFrameworkCore
Imports Microsoft.Extensions.DependencyInjection
Imports Microsoft.Extensions.Hosting
Imports Microsoft.Extensions.Logging
Imports IOCLCommunityHall.Data
Imports IOCLCommunityHall.Models.Entities

Namespace Services
    ''' <summary>
    ''' Autonomous background service that automatically releases inventory stock
    ''' back to the available pool when a rental allocation's EndDate has passed.
    '''
    ''' Schedule: Runs once at startup, then every hour.
    '''
    ''' For each expired allocation (Status = "Approved" / "Reserved", EndDate < Today):
    '''   1. Decreases item.ReservedQuantity by the allocated amount
    '''   2. Marks the allocation Status = "Released"
    '''   3. Marks the RentalRequest Status = Returned (5)
    '''   4. Logs a TransactionType.Release entry in InventoryTransactions
    '''   5. Sends a notification to the requester
    ''' </summary>
    Public Class InventoryReleaseService
        Inherits BackgroundService

        Private ReadOnly _serviceProvider As IServiceProvider
        Private ReadOnly _logger As ILogger(Of InventoryReleaseService)

        ' Run the check every hour
        Private ReadOnly _interval As TimeSpan = TimeSpan.FromHours(1)

        Public Sub New(serviceProvider As IServiceProvider,
                       logger As ILogger(Of InventoryReleaseService))
            _serviceProvider = serviceProvider
            _logger = logger
        End Sub

        Protected Overrides Async Function ExecuteAsync(stoppingToken As CancellationToken) As Task
            _logger.LogInformation("[InventoryReleaseService] Autonomous stock-release service started.")

            ' Run immediately on startup, then wait for the interval
            Do
                Try
                    Await ReleaseExpiredAllocationsAsync(stoppingToken)
                Catch ex As Exception When Not TypeOf ex Is OperationCanceledException
                    _logger.LogError(ex, "[InventoryReleaseService] Unhandled error during release cycle.")
                End Try

                ' Wait for next cycle (or stop if cancellation requested)
                Try
                    Await Task.Delay(_interval, stoppingToken)
                Catch ex As TaskCanceledException
                    Exit Do
                End Try
            Loop

            _logger.LogInformation("[InventoryReleaseService] Service stopped.")
        End Function

        Private Async Function ReleaseExpiredAllocationsAsync(token As CancellationToken) As Task
            ' Use a fresh DI scope per cycle (BackgroundService is singleton, DbContext is scoped)
            Using scope = _serviceProvider.CreateScope()
                Dim context = scope.ServiceProvider.GetRequiredService(Of ApplicationDbContext)()
                Dim notifService = scope.ServiceProvider.GetRequiredService(Of INotificationService)()

                Dim today = DateTime.UtcNow.Date

                ' Find all active allocations whose end date is strictly in the past
                Dim expiredAllocations = Await context.InventoryAllocations.
                    Where(Function(a) (a.Status = "Approved" OrElse a.Status = "Reserved") AndAlso
                                       a.EndDate.Date < today).
                    Include(Function(a) a.RentalRequest).
                    ToListAsync(token)

                If Not expiredAllocations.Any() Then
                    _logger.LogDebug("[InventoryReleaseService] No expired allocations found at {Now}.", DateTime.UtcNow)
                    Return
                End If

                _logger.LogInformation("[InventoryReleaseService] Found {Count} expired allocation(s) to release.", expiredAllocations.Count)

                ' Group by request so we process each request once
                Dim byRequest = expiredAllocations.GroupBy(Function(a) a.RequestId).ToList()

                For Each group In byRequest
                    Dim requestId = group.Key
                    Dim request = group.First().RentalRequest

                    ' Release each allocation in this request
                    For Each alloc In group
                        Dim item = Await context.InventoryItems.FindAsync({alloc.InventoryItemId}, token)
                        If item Is Nothing Then Continue For

                        Dim released = alloc.AllocatedQuantity
                        Dim before = item.ReservedQuantity

                        ' Decrease reserved quantity — never go below 0
                        item.ReservedQuantity = Math.Max(0, item.ReservedQuantity - released)
                        item.UpdatedAt = DateTime.UtcNow

                        ' Mark allocation as released
                        alloc.Status = "Released"

                        ' Log the release transaction
                        Await context.InventoryTransactions.AddAsync(New InventoryTransaction With {
                            .InventoryItemId = item.Id,
                            .RentalRequestId = requestId,
                            .TransactionType = TransactionType.Release,
                            .QuantityChanged = released,
                            .QuantityBefore = before,
                            .QuantityAfter = item.ReservedQuantity,
                            .Notes = $"Auto-released: allocation expired (EndDate {alloc.EndDate:dd MMM yyyy}). Request #{If(request IsNot Nothing, request.RequestNumber, requestId.ToString())}.",
                            .PerformedBy = "SYSTEM (Auto-Release)"
                        }, token)

                        _logger.LogInformation(
                            "[InventoryReleaseService] Released {Qty} unit(s) of '{Item}' from request #{Req} (EndDate: {End:dd-MMM-yyyy}).",
                            released, item.Name, If(request IsNot Nothing, request.RequestNumber, requestId.ToString()), alloc.EndDate)
                    Next

                    ' Update request status to Returned
                    If request IsNot Nothing AndAlso
                       (request.Status = RequestStatus.Approved OrElse request.Status = RequestStatus.Pending) Then
                        request.Status = RequestStatus.Returned
                        request.ReviewedAt = DateTime.UtcNow
                        request.ReviewedByEmployeeId = "SYSTEM"

                        ' Notify the requester
                        Try
                            Await notifService.SendNotificationAsync(
                                request.UserId,
                                "Rental Period Ended — Items Released ✓",
                                $"Your rental request #{request.RequestNumber} has ended. All allocated inventory has been automatically released back to stock. Thank you!",
                                NotificationType.Approved,
                                "/RentalRequest/MyRequests")
                        Catch ex As Exception
                            _logger.LogWarning(ex, "[InventoryReleaseService] Could not send notification for request #{Req}.", request.RequestNumber)
                        End Try
                    End If
                Next

                Await context.SaveChangesAsync(token)

                _logger.LogInformation("[InventoryReleaseService] Cycle complete. {Count} allocation group(s) processed and stock released.", byRequest.Count)
            End Using
        End Function
    End Class
End Namespace
