Imports Microsoft.EntityFrameworkCore
Imports Microsoft.Extensions.Logging
Imports IOCLCommunityHall.Data
Imports IOCLCommunityHall.Models.Entities

Namespace Services
    ''' <summary>
    ''' Scoped service that scans all Approved rental requests whose rental period has ended
    ''' and releases the reserved inventory back to the available pool.
    '''
    ''' RELEASE CONDITION:
    '''   DateTime.UtcNow >= EndDate.Date.AddDays(1).AddHours(6)   (next day 06:00 AM UTC)
    '''   AND request.InventoryReleased = False                     (duplicate-release guard)
    '''
    ''' WHAT IT DOES PER REQUEST:
    '''   1. Check InventoryReleased = False (skip if already released)
    '''   2. For each allocation: decrease item.ReservedQuantity by AllocatedQuantity (min 0)
    '''   3. Mark allocation.Status = "Released"
    '''   4. Log InventoryTransaction (TransactionType.Release)
    '''   5. Set request.InventoryReleased = True, request.InventoryReleasedAt = UtcNow
    '''   6. Send notification to requester
    '''   NOTE: Request.Status is NOT changed — remains Approved.
    '''
    ''' EDGE CASES HANDLED:
    '''   - Multiple items per request (batch processed)
    '''   - Multiple requests ending same day (all processed in one call)
    '''   - Cancelled / Rejected requests (allocations already Released — condition filters them)
    '''   - ReservedQty never goes below 0 (Math.Max guard)
    '''   - Duplicate execution on same request is a no-op (InventoryReleased = True guard)
    ''' </summary>
    Public Class InventoryReleaseEngine
        Implements IInventoryReleaseEngine

        Private ReadOnly _context As ApplicationDbContext
        Private ReadOnly _notificationService As INotificationService
        Private ReadOnly _logger As ILogger(Of InventoryReleaseEngine)

        Public Sub New(context As ApplicationDbContext,
                       notificationService As INotificationService,
                       logger As ILogger(Of InventoryReleaseEngine))
            _context = context
            _notificationService = notificationService
            _logger = logger
        End Sub

        ''' <summary>
        ''' Release all expired allocations.
        ''' Returns the number of RentalRequests whose inventory was released in this call.
        ''' </summary>
        Public Async Function TriggerReleaseAsync() As Task(Of Integer) Implements IInventoryReleaseEngine.TriggerReleaseAsync
            Dim utcNow = DateTime.UtcNow
            Dim istZone As TimeZoneInfo
            Try
                istZone = TimeZoneInfo.FindSystemTimeZoneById("India Standard Time")
            Catch
                istZone = TimeZoneInfo.CreateCustomTimeZone("IST", New TimeSpan(5, 30, 0), "India Standard Time", "India Standard Time")
            End Try
            Dim istNow = TimeZoneInfo.ConvertTimeFromUtc(utcNow, istZone)
            Dim releasedCount As Integer = 0

            ' ── Find all ACTIVE allocations that belong to requests whose rental period ended ──
            ' Release condition: istNow >= EndDate.Date + 1 day + 06 hours
            ' We query requests first (not allocations) so we can check InventoryReleased flag.
            Dim expiredRequests = Await _context.RentalRequests.
                Where(Function(r) r.Status = RequestStatus.Approved AndAlso
                                   Not r.InventoryReleased).
                Include(Function(r) r.InventoryAllocations).
                ThenInclude(Function(a) a.InventoryItem).
                ToListAsync()

            ' Filter to requests whose release time has actually arrived
            Dim dueRequests = expiredRequests.
                Where(Function(r) istNow >= r.EndDate.Date.AddDays(1).AddHours(6)).
                ToList()

            If Not dueRequests.Any() Then
                _logger.LogDebug("[InventoryReleaseEngine] No expired reservations due for release at IST {IstNow:dd-MMM-yyyy HH:mm}.", istNow)
                Return 0
            End If

            _logger.LogInformation("[InventoryReleaseEngine] {Count} request(s) due for inventory release.", dueRequests.Count)

            For Each request In dueRequests
                ' Secondary guard: skip if already released (thread-safety in concurrent calls)
                If request.InventoryReleased Then Continue For

                Dim activeAllocations = request.InventoryAllocations.
                    Where(Function(a) a.Status = "Approved" OrElse a.Status = "Reserved").
                    ToList()

                If Not activeAllocations.Any() Then
                    ' Nothing to release — just mark as released so we don't re-process
                    request.InventoryReleased = True
                    request.InventoryReleasedAt = utcNow
                    Continue For
                End If

                For Each alloc In activeAllocations
                    Dim item = alloc.InventoryItem
                    If item Is Nothing Then
                        ' Reload from DB if navigation property not populated
                        item = Await _context.InventoryItems.FindAsync(alloc.InventoryItemId)
                        If item Is Nothing Then Continue For
                    End If

                    Dim qtyReleased = alloc.AllocatedQuantity
                    Dim reservedBefore = item.ReservedQuantity

                    ' FORMULA: ReservedQty = MAX(0, ReservedQty - AllocatedQty)
                    ' AvailableQty is computed: TotalQuantity - ReservedQuantity (not stored separately)
                    item.ReservedQuantity = Math.Max(0, item.ReservedQuantity - qtyReleased)
                    item.UpdatedAt = utcNow

                    ' Mark allocation as released
                    alloc.Status = "Released"

                    ' Log the release transaction (full audit trail)
                    _context.InventoryTransactions.Add(New InventoryTransaction With {
                        .InventoryItemId = item.Id,
                        .RentalRequestId = request.Id,
                        .TransactionType = TransactionType.Release,
                        .QuantityChanged = qtyReleased,
                        .QuantityBefore = reservedBefore,
                        .QuantityAfter = item.ReservedQuantity,
                        .Notes = $"Auto-release: rental period ended (EndDate {request.EndDate:dd-MMM-yyyy}). " &
                                 $"Request #{request.RequestNumber}. " &
                                 $"Released {qtyReleased} unit(s) of '{item.Name}'.",
                        .PerformedBy = "SYSTEM (Auto-Release Engine)",
                        .CreatedAt = utcNow
                    })

                    _logger.LogInformation(
                        "[InventoryReleaseEngine] Released {Qty} unit(s) of '{Item}' (ItemId={ItemId}) " &
                        "from request #{Req} (EndDate: {End:dd-MMM-yyyy}). " &
                        "Reserved: {Before} → {After}.",
                        qtyReleased, item.Name, item.Id,
                        request.RequestNumber, request.EndDate,
                        reservedBefore, item.ReservedQuantity)
                Next

                ' Mark the request as inventory-released
                ' NOTE: request.Status is NOT changed — it remains Approved.
                request.InventoryReleased = True
                request.InventoryReleasedAt = utcNow

                ' Notify the requester
                Try
                    Await _notificationService.SendNotificationAsync(
                        request.UserId,
                        "Rental Period Ended — Inventory Released",
                        $"Your rental request #{request.RequestNumber} (period: " &
                        $"{request.StartDate:dd-MMM-yyyy} to {request.EndDate:dd-MMM-yyyy}) has ended. " &
                        $"All allocated inventory has been automatically released back to stock.",
                        NotificationType.Approved,
                        "/RentalRequest/MyRequests")
                Catch ex As Exception
                    _logger.LogWarning(ex, "[InventoryReleaseEngine] Could not send notification for request #{Req}.", request.RequestNumber)
                End Try

                releasedCount += 1
            Next

            If releasedCount > 0 Then
                Await _context.SaveChangesAsync()
                _logger.LogInformation("[InventoryReleaseEngine] Release cycle complete. {Count} request(s) processed.", releasedCount)
            End If

            Return releasedCount
        End Function
    End Class
End Namespace
