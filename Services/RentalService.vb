Imports System.IO
Imports Microsoft.EntityFrameworkCore
Imports Microsoft.AspNetCore.Identity
Imports IOCLCommunityHall.Data
Imports IOCLCommunityHall.Models.Entities
Imports IOCLCommunityHall.Models.ViewModels
Imports IOCLCommunityHall.Repositories

Namespace Services
    ''' <summary>
    ''' Core rental service implementing FCFS allocation logic and multi-level approval workflow.
    '''
    ''' APPROVAL THRESHOLD: ₹10,000
    ''' ┌─────────────────┬──────────────┬──────────────────────────────────────────┐
    ''' │ Submitted By    │ Amount       │ Approval Route                           │
    ''' ├─────────────────┼──────────────┼──────────────────────────────────────────┤
    ''' │ Employee (User) │ Any          │ HOD → HR (≤10k) / HOD → GM → HR (>10k) │
    ''' │ HOD             │ ≤ ₹10,000   │ HR (SuperAdmin) directly                 │
    ''' │ HOD             │ > ₹10,000   │ GM → HR (SuperAdmin)                     │
    ''' │ GM              │ Any          │ HR (SuperAdmin) directly                 │
    ''' │ SuperAdmin (HR) │ Any          │ GM directly                              │
    ''' └─────────────────┴──────────────┴──────────────────────────────────────────┘
    ''' SELF-APPROVAL: A user can NEVER approve their own request (blocked in ApproveRequestAsync).
    ''' </summary>
    Public Class RentalService
        Implements IRentalService

        Private Const ApprovalThreshold As Decimal = 10000D

        Private ReadOnly _requestRepo As IRentalRequestRepository
        Private ReadOnly _inventoryRepo As IInventoryRepository
        Private ReadOnly _context As ApplicationDbContext
        Private ReadOnly _notificationService As INotificationService
        Private ReadOnly _auditService As IAuditService
        Private ReadOnly _userManager As UserManager(Of ApplicationUser)

        Public Sub New(requestRepo As IRentalRequestRepository,
                       inventoryRepo As IInventoryRepository,
                       context As ApplicationDbContext,
                       notificationService As INotificationService,
                       auditService As IAuditService,
                       userManager As UserManager(Of ApplicationUser))
            _requestRepo = requestRepo
            _inventoryRepo = inventoryRepo
            _context = context
            _notificationService = notificationService
            _auditService = auditService
            _userManager = userManager
        End Sub

        Public Async Function GetAllRequestsAsync() As Task(Of IEnumerable(Of RentalRequest)) Implements IRentalService.GetAllRequestsAsync
            Return Await _requestRepo.GetAllRequestsWithDetailsAsync()
        End Function

        Public Async Function GetRequestByIdAsync(id As Integer) As Task(Of RentalRequest) Implements IRentalService.GetRequestByIdAsync
            Return Await _requestRepo.GetRequestWithItemsAsync(id)
        End Function

        Public Async Function GetUserRequestsAsync(userId As String) As Task(Of IEnumerable(Of RentalRequest)) Implements IRentalService.GetUserRequestsAsync
            Return Await _requestRepo.GetUserRequestsAsync(userId)
        End Function

        Public Async Function GetFCFSQueueAsync() As Task(Of IEnumerable(Of RentalRequest)) Implements IRentalService.GetFCFSQueueAsync
            Return Await _requestRepo.GetPendingRequestsOrderedByFCFSAsync()
        End Function

        ''' <summary>
        ''' Returns requests that the given approver can act on for their stage,
        ''' excluding any requests they themselves submitted (self-approval guard).
        ''' </summary>
        Public Async Function GetRequestsForApproverAsync(userId As String, approverRole As String) As Task(Of IEnumerable(Of RentalRequest)) Implements IRentalService.GetRequestsForApproverAsync
            Dim stageForRole = GetStageForRole(approverRole)
            Dim all = Await _requestRepo.GetAllRequestsWithDetailsAsync()
            Return all.Where(Function(r) r.ApprovalStage = stageForRole AndAlso
                                          r.Status = RequestStatus.Pending AndAlso
                                          r.UserId <> userId)
        End Function

        ''' <summary>
        ''' Determines the first ApprovalStage for a new request based on who is submitting it and the total amount.
        ''' </summary>
        Public Function DetermineInitialStage(submitterRole As String, grandTotal As Decimal) As ApprovalStage Implements IRentalService.DetermineInitialStage
            Select Case submitterRole
                Case "User"         ' Employee
                    Return ApprovalStage.PendingHOD

                Case "HOD"
                    ' HOD's own request skips HOD stage entirely
                    If grandTotal <= ApprovalThreshold Then
                        Return ApprovalStage.PendingHR  ' HR approves directly
                    Else
                        Return ApprovalStage.PendingGM  ' GM → HR
                    End If

                Case "GM"
                    ' GM requests are auto-approved on submission — no further approval needed.
                    Return ApprovalStage.Approved

                Case "SuperAdmin"   ' HR submits — route by amount
                    If grandTotal <= ApprovalThreshold Then
                        ' SuperAdmin IS the HR admin — auto-approve to avoid self-approval loop
                        Return ApprovalStage.Approved
                    Else
                        Return ApprovalStage.PendingGM  ' High-value → GM
                    End If

                Case Else
                    Return ApprovalStage.PendingHOD
            End Select
        End Function

        ''' <summary>What stage corresponds to a given approver role?</summary>
        Private Function GetStageForRole(approverRole As String) As ApprovalStage
            Select Case approverRole
                Case "HOD"        : Return ApprovalStage.PendingHOD
                Case "GM"         : Return ApprovalStage.PendingGM
                Case "SuperAdmin" : Return ApprovalStage.PendingHR
                Case Else         : Return ApprovalStage.PendingHOD
            End Select
        End Function

        ''' <summary>
        ''' What is the next stage after the current approver approves?
        ''' Returns Nothing if this is the final stage (request becomes Approved).
        ''' Also considers whether the employee submitted the original request
        ''' so we know if an HR-submitted request (GM approves) is final.
        ''' </summary>
        Private Function GetNextStage(currentStage As ApprovalStage, submittedByRole As String, grandTotal As Decimal) As ApprovalStage?
            Select Case currentStage
                Case ApprovalStage.PendingHOD
                    ' After HOD approves: low-value → HR directly, high-value → GM
                    If grandTotal <= ApprovalThreshold Then
                        Return ApprovalStage.PendingHR
                    Else
                        Return ApprovalStage.PendingGM
                    End If

                Case ApprovalStage.PendingGM
                    ' GM approval is terminal for requests above threshold
                    Return Nothing

                Case ApprovalStage.PendingHR
                    ' HR approval is terminal for requests at or below threshold
                    Return Nothing

                Case Else
                    Return Nothing
            End Select
        End Function

        Public Async Function CreateRequestAsync(vm As RentalRequestViewModel, userId As String, submitterRole As String) As Task(Of RentalRequest) Implements IRentalService.CreateRequestAsync
            ' Validate date range
            If vm.StartDate.Date < DateTime.Today Then
                Throw New InvalidOperationException("Start date cannot be in the past.")
            End If
            If vm.EndDate.Date < vm.StartDate.Date Then
                Throw New InvalidOperationException("Item Required Until date cannot be earlier than Item Required From date.")
            End If

            ' 1. Calculate duration in days (matching frontend logic)
            Dim days As Integer = 1
            Dim timeDiff = vm.EndDate.Date - vm.StartDate.Date
            If timeDiff.Days > 0 Then days = timeDiff.Days

            ' Validate that there are no duplicate items within this request
            Dim activeItemIds = vm.RequestItems.Where(Function(ri) ri.RequestedQuantity > 0).Select(Function(ri) ri.InventoryItemId).ToList()
            If activeItemIds.Count <> activeItemIds.Distinct().Count() Then
                Throw New InvalidOperationException("Duplicate items are not allowed in the same rental request.")
            End If

            ' Allow multiple active requests for the same item. Real-time availability checks below will handle stock limits.

            ' 2. Pre-calculate grand total using actual database prices
            Dim grandTotal As Decimal = 0
            Dim itemsToSave As New List(Of RentalRequestItem)()

            For Each itemVm In vm.RequestItems
                If itemVm.RequestedQuantity > 0 Then
                    Dim inventoryItem = Await _inventoryRepo.GetByIdAsync(itemVm.InventoryItemId)
                    If inventoryItem IsNot Nothing Then
                        ' Validate stock availability for the date range
                        Dim available = Await GetAvailableQuantityForDatesAsync(itemVm.InventoryItemId, vm.StartDate, vm.EndDate, 0)
                        If itemVm.RequestedQuantity > available Then
                            Throw New InvalidOperationException($"Requested quantity ({itemVm.RequestedQuantity}) for item '{inventoryItem.Name}' exceeds available stock ({available}) for the selected date range.")
                        End If

                        grandTotal += CDec(itemVm.RequestedQuantity) * inventoryItem.CurrentPrice

                        Dim lineItem = New RentalRequestItem With {
                            .InventoryItemId = itemVm.InventoryItemId,
                            .RequestedQuantity = itemVm.RequestedQuantity,
                            .UnitPriceAtRequest = inventoryItem.CurrentPrice,
                            .Status = ItemRequestStatus.Pending
                        }
                        itemsToSave.Add(lineItem)
                    End If
                End If
            Next

            grandTotal = grandTotal * days

            ' 3. Handle document upload
            Dim docPath = String.Empty
            If vm.InPrincipalDocumentFile IsNot Nothing AndAlso vm.InPrincipalDocumentFile.Length > 0 Then
                docPath = Await SaveDocumentAsync(vm.InPrincipalDocumentFile)
            End If

            ' 4. Determine initial approval stage based on submitter role
            Dim initialStage = DetermineInitialStage(submitterRole, grandTotal)

            ' GM and SuperAdmin (≤₹10,000) requests are auto-approved:
            ' - GM has no approver above them for their own requests
            ' - SuperAdmin IS the HR admin (self-approval prevention)
            Dim isAutoApprove = (submitterRole = "GM") OrElse
                                (submitterRole = "SuperAdmin" AndAlso grandTotal <= ApprovalThreshold)

            Dim request = New RentalRequest With {
                .RequestNumber = Await _requestRepo.GenerateRequestNumberAsync(),
                .UserId = userId,
                .SubmittedByRole = submitterRole,
                .EventDate = vm.EventDate,
                .StartDate = vm.StartDate,
                .EndDate = vm.EndDate,
                .InPrincipalDocumentPath = docPath,
                .GrandTotal = grandTotal,
                .Status = If(isAutoApprove, RequestStatus.Approved, RequestStatus.Pending),
                .ApprovalStage = initialStage,
                .CreatedAt = DateTime.UtcNow,
                .ReviewedAt = If(isAutoApprove, CType(DateTime.UtcNow, DateTime?), Nothing),
                .ReviewedByEmployeeId = If(isAutoApprove, "SYSTEM", String.Empty)
            }

            Await _requestRepo.AddAsync(request)
            Await _requestRepo.SaveAsync()

            ' 5. Add line items
            For Each lineItem In itemsToSave
                lineItem.RentalRequestId = request.Id
                Await _context.RentalRequestItems.AddAsync(lineItem)
            Next
            Await _context.SaveChangesAsync()

            ' 6a. Auto-approve: GM (any amount) or SuperAdmin (≤₹10,000) — allocate inventory immediately
            If isAutoApprove Then
                For Each lineItem In request.RentalRequestItems
                    Dim item = Await _inventoryRepo.GetByIdAsync(lineItem.InventoryItemId)
                    If item Is Nothing Then Continue For

                    Dim available = Await GetAvailableQuantityForDatesAsync(lineItem.InventoryItemId, request.StartDate, request.EndDate, request.Id)
                    Dim canAllocate = Math.Min(lineItem.RequestedQuantity, available)
                    lineItem.AllocatedQuantity = canAllocate

                    If canAllocate >= lineItem.RequestedQuantity Then
                        lineItem.Status = ItemRequestStatus.FullyAllocated
                    ElseIf canAllocate > 0 Then
                        lineItem.Status = ItemRequestStatus.PartiallyAllocated
                        lineItem.StatusReason = $"Only {canAllocate} of {lineItem.RequestedQuantity} available (FCFS)"
                    Else
                        lineItem.Status = ItemRequestStatus.Rejected
                        lineItem.StatusReason = "No stock available"
                    End If

                    If canAllocate > 0 Then
                        item.ReservedQuantity += canAllocate
                        item.UpdatedAt = DateTime.UtcNow

                        Dim allocation = New InventoryAllocation With {
                            .RequestId = request.Id,
                            .InventoryItemId = item.Id,
                            .AllocatedQuantity = canAllocate,
                            .StartDate = request.StartDate,
                            .EndDate = request.EndDate,
                            .Status = "Approved",
                            .AllocationDate = DateTime.UtcNow
                        }
                        Await _context.InventoryAllocations.AddAsync(allocation)

                        Await _context.InventoryTransactions.AddAsync(New InventoryTransaction With {
                            .InventoryItemId = item.Id,
                            .RentalRequestId = request.Id,
                            .TransactionType = TransactionType.Allocation,
                            .QuantityChanged = canAllocate,
                            .QuantityBefore = item.ReservedQuantity - canAllocate,
                            .QuantityAfter = item.ReservedQuantity,
                            .Notes = $"Auto-allocated for {submitterRole} request {request.RequestNumber}",
                            .PerformedBy = $"Auto-Approved ({submitterRole})"
                        })
                    End If
                Next
                Await _context.SaveChangesAsync()

                ' Notify requester that their request was auto-approved
                Await _notificationService.SendNotificationAsync(userId,
                    "Request Auto-Approved ✓",
                    $"Your rental request #{request.RequestNumber} has been automatically approved and inventory allocated.",
                    NotificationType.Approved,
                    "/RentalRequest/MyRequests")

                Return request
            End If

            ' 6b. Notify the appropriate approver(s) for non-GM requests
            Dim notifTitle = $"New Rental Request — Action Required"
            Dim notifMsg = $"Request #{request.RequestNumber} submitted by {submitterRole} requires your approval."
            Dim notifLink = $"/AdminRequest/Details/{request.Id}"

            Select Case initialStage
                Case ApprovalStage.PendingHOD
                    Await _notificationService.SendToRoleAsync("HOD", notifTitle, notifMsg, NotificationType.NewRequest, notifLink)
                Case ApprovalStage.PendingGM
                    Await _notificationService.SendToRoleAsync("GM", notifTitle, notifMsg, NotificationType.NewRequest, notifLink)
                Case ApprovalStage.PendingHR
                    Await _notificationService.SendToRoleAsync("SuperAdmin", notifTitle, notifMsg, NotificationType.NewRequest, notifLink)
            End Select

            Return request
        End Function

        ''' <summary>
        ''' Multi-stage approval with FCFS inventory allocation on final approval.
        ''' Self-approval is always blocked — approverUser cannot be the same as request.UserId.
        ''' </summary>
        Public Async Function ApproveRequestAsync(requestId As Integer, approverUser As String, approverRole As String) As Task(Of (Success As Boolean, Message As String)) Implements IRentalService.ApproveRequestAsync
            Dim request = Await _requestRepo.GetRequestWithItemsAsync(requestId)
            If request Is Nothing Then Return (False, "Request not found.")
            If request.Status <> RequestStatus.Pending Then Return (False, "Request is not in Pending status.")

            ' ── Self-Approval Guard ────────────────────────────────────────────────
            If request.UserId = approverUser Then
                Return (False, "You cannot approve your own request.")
            End If

            ' ── Stage Validation ──────────────────────────────────────────────────
            Dim expectedStage = GetStageForRole(approverRole)
            If request.ApprovalStage <> expectedStage Then
                Return (False, $"This request is currently at stage '{request.ApprovalStage}' and cannot be approved by a {approverRole}.")
            End If

            ' ── Record this stage's approval ──────────────────────────────────────
            Dim approverInfo As ApplicationUser = Await _userManager.FindByIdAsync(approverUser)
            Dim approverName = If(approverInfo?.FullName, approverUser)
            Dim approverEmployeeId = If(approverInfo?.EmployeeId, If(approverInfo?.UserName, approverUser))

            Select Case request.ApprovalStage
                Case ApprovalStage.PendingHOD
                    request.HODApprovedAt = DateTime.UtcNow
                    request.HODApprovedByEmployeeId = approverEmployeeId
                Case ApprovalStage.PendingGM
                    request.GMApprovedAt = DateTime.UtcNow
                    request.GMApprovedByEmployeeId = approverEmployeeId
                Case ApprovalStage.PendingHR
                    request.HRApprovedAt = DateTime.UtcNow
                    request.HRApprovedByEmployeeId = approverEmployeeId
            End Select

            ' ── Advance to Next Stage or Fully Approve ────────────────────────────
            Dim nextStageOpt = GetNextStage(request.ApprovalStage, request.SubmittedByRole, request.GrandTotal)

            If nextStageOpt.HasValue Then
                ' More stages remain — advance
                request.ApprovalStage = nextStageOpt.Value
                Await _context.SaveChangesAsync()

                ' Notify the next approver
                Dim nextRole As String
                Select Case nextStageOpt.Value
                    Case ApprovalStage.PendingGM : nextRole = "GM"
                    Case ApprovalStage.PendingHR : nextRole = "SuperAdmin"
                    Case Else : nextRole = "SuperAdmin"
                End Select

                Await _notificationService.SendToRoleAsync(nextRole,
                    $"Rental Request — Awaiting Your Approval",
                    $"Request #{request.RequestNumber} has been approved by {approverRole} ({approverName}) and now requires your review.",
                    NotificationType.NewRequest,
                    $"/AdminRequest/Details/{requestId}")

                ' Notify requester of progress
                Await _notificationService.SendNotificationAsync(request.UserId,
                    $"Request Update — Stage {approverRole} Approved",
                    $"Your request #{request.RequestNumber} was approved by {approverRole}. Next: awaiting {nextRole} approval.",
                    NotificationType.Approved,
                    "/RentalRequest/MyRequests")

                Return (True, $"Stage approved by {approverRole}. Request forwarded to {nextRole} for next approval.")
            Else
                ' ── Final Approval — Validate Stock First (Overlapping dates check) ──
                For Each lineItem In request.RentalRequestItems
                    Dim available = Await GetAvailableQuantityForDatesAsync(lineItem.InventoryItemId, request.StartDate, request.EndDate, request.Id)
                    If lineItem.RequestedQuantity > available Then
                        Return (False, "Requested inventory is no longer available for the selected date range")
                    End If
                Next

                ' ── Final Approval — Allocate Inventory ──
                For Each lineItem In request.RentalRequestItems
                    Dim item = Await _inventoryRepo.GetByIdAsync(lineItem.InventoryItemId)
                    If item Is Nothing Then Continue For

                    Dim available = Await GetAvailableQuantityForDatesAsync(lineItem.InventoryItemId, request.StartDate, request.EndDate, request.Id)
                    Dim canAllocate = Math.Min(lineItem.RequestedQuantity, available)

                    lineItem.AllocatedQuantity = canAllocate
                    lineItem.Status = ItemRequestStatus.FullyAllocated

                    If canAllocate > 0 Then
                        item.ReservedQuantity += canAllocate
                        item.UpdatedAt = DateTime.UtcNow

                        Dim allocation = New InventoryAllocation With {
                            .RequestId = request.Id,
                            .InventoryItemId = item.Id,
                            .AllocatedQuantity = canAllocate,
                            .StartDate = request.StartDate,
                            .EndDate = request.EndDate,
                            .Status = "Approved",
                            .AllocationDate = DateTime.UtcNow
                        }
                        Await _context.InventoryAllocations.AddAsync(allocation)

                        Await _context.InventoryTransactions.AddAsync(New InventoryTransaction With {
                            .InventoryItemId = item.Id,
                            .RentalRequestId = request.Id,
                            .TransactionType = TransactionType.Allocation,
                            .QuantityChanged = canAllocate,
                            .QuantityBefore = item.ReservedQuantity - canAllocate,
                            .QuantityAfter = item.ReservedQuantity,
                            .Notes = $"Allocated for request {request.RequestNumber}",
                            .PerformedBy = approverName
                        })
                    End If
                Next

                request.Status = RequestStatus.Approved
                request.ApprovalStage = ApprovalStage.Approved
                request.ReviewedAt = DateTime.UtcNow
                request.ReviewedByEmployeeId = approverEmployeeId

                Await _context.SaveChangesAsync()

                Await _notificationService.SendNotificationAsync(request.UserId,
                    "Request Fully Approved ✓",
                    $"Your rental request #{request.RequestNumber} has been fully approved and inventory allocated.",
                    NotificationType.Approved,
                    "/RentalRequest/MyRequests")

                Return (True, "Request fully approved and inventory allocated.")
            End If
        End Function

        Public Async Function RejectRequestAsync(requestId As Integer, reason As String, adminUserName As String) As Task(Of Boolean) Implements IRentalService.RejectRequestAsync
            Dim request = Await _requestRepo.GetRequestWithItemsAsync(requestId)
            If request Is Nothing OrElse request.Status <> RequestStatus.Pending Then Return False

            request.Status = RequestStatus.Rejected
            request.ApprovalStage = ApprovalStage.Rejected
            request.ReviewedAt = DateTime.UtcNow
            request.ReviewedByEmployeeId = adminUserName
            request.RejectionReason = reason

            For Each lineItem In request.RentalRequestItems
                lineItem.Status = ItemRequestStatus.Rejected
                lineItem.StatusReason = reason
            Next

            Await _context.SaveChangesAsync()

            Await _notificationService.SendNotificationAsync(request.UserId,
                "Request Rejected",
                $"Your rental request #{request.RequestNumber} has been rejected. Reason: {reason}",
                NotificationType.Rejected,
                "/RentalRequest/MyRequests")

            Return True
        End Function

        Private Async Function SaveDocumentAsync(file As Microsoft.AspNetCore.Http.IFormFile) As Task(Of String)
            Dim uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "documents")
            Directory.CreateDirectory(uploadsFolder)
            Dim ext = Path.GetExtension(file.FileName)
            Dim fileName = $"{Guid.NewGuid()}{ext}"
            Dim filePath = Path.Combine(uploadsFolder, fileName)
            Using stream = New FileStream(filePath, FileMode.Create)
                Await file.CopyToAsync(stream)
            End Using
            Return $"/uploads/documents/{fileName}"
        End Function

        Public Async Function CancelRequestAsync(requestId As Integer, userName As String) As Task(Of Boolean) Implements IRentalService.CancelRequestAsync
            Dim request = Await _requestRepo.GetRequestWithItemsAsync(requestId)
            If request Is Nothing Then Return False
            If request.Status <> RequestStatus.Pending AndAlso request.Status <> RequestStatus.Approved Then Return False

            If request.Status = RequestStatus.Approved Then
                Dim allocations = Await _context.InventoryAllocations.
                    Where(Function(a) a.RequestId = requestId).ToListAsync()

                For Each alloc In allocations
                    Dim item = Await _inventoryRepo.GetByIdAsync(alloc.InventoryItemId)
                    If item IsNot Nothing Then
                        item.ReservedQuantity = Math.Max(0, item.ReservedQuantity - alloc.AllocatedQuantity)
                        item.UpdatedAt = DateTime.UtcNow

                        Await _context.InventoryTransactions.AddAsync(New InventoryTransaction With {
                            .InventoryItemId = item.Id,
                            .RentalRequestId = requestId,
                            .TransactionType = TransactionType.Release,
                            .QuantityChanged = alloc.AllocatedQuantity,
                            .QuantityBefore = item.ReservedQuantity + alloc.AllocatedQuantity,
                            .QuantityAfter = item.ReservedQuantity,
                            .Notes = $"Released allocation due to cancellation of request {request.RequestNumber}",
                            .PerformedBy = userName
                        })
                    End If
                    _context.InventoryAllocations.Remove(alloc)
                Next
            End If

            request.Status = RequestStatus.Cancelled
            request.ApprovalStage = ApprovalStage.Rejected
            request.ReviewedAt = DateTime.UtcNow
            request.ReviewedByEmployeeId = userName

            Await _context.SaveChangesAsync()

            Await _notificationService.SendNotificationAsync(request.UserId,
                "Request Cancelled",
                $"Your rental request #{request.RequestNumber} has been cancelled successfully.",
                NotificationType.Rejected,
                "/RentalRequest/MyRequests")

            Return True
        End Function

        Public Async Function GetAvailableQuantityForDatesAsync(itemId As Integer, startDate As DateTime, endDate As DateTime, excludeRequestId As Integer) As Task(Of Integer) Implements IRentalService.GetAvailableQuantityForDatesAsync
            Dim item = Await _context.InventoryItems.FindAsync(itemId)
            If item Is Nothing Then Return 0

            Dim maxReserved As Integer = 0
            Dim currentDate = startDate.Date
            Dim finalDate = endDate.Date
            
            While currentDate <= finalDate
                Dim reservedOnDay = Await _context.InventoryAllocations.
                    Where(Function(a) a.InventoryItemId = itemId AndAlso
                                      a.RequestId <> excludeRequestId AndAlso
                                      (a.Status = "Approved" OrElse a.Status = "Reserved") AndAlso
                                      a.StartDate <= currentDate AndAlso
                                      a.EndDate >= currentDate).
                    SumAsync(Function(a) CType(a.AllocatedQuantity, Integer?))
                
                Dim dayReserved = If(reservedOnDay, 0)
                If dayReserved > maxReserved Then
                    maxReserved = dayReserved
                End If
                
                currentDate = currentDate.AddDays(1)
            End While

            Return Math.Max(0, item.TotalQuantity - maxReserved)
        End Function
    End Class
End Namespace
