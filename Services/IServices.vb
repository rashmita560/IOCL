Imports IOCLCommunityHall.Models.Entities
Imports IOCLCommunityHall.Models.ViewModels

Namespace Services
    Public Interface IRentalService
        Function GetAllRequestsAsync() As Task(Of IEnumerable(Of RentalRequest))
        Function GetRequestByIdAsync(id As Integer) As Task(Of RentalRequest)
        Function GetUserRequestsAsync(userId As String) As Task(Of IEnumerable(Of RentalRequest))
        ''' <summary>Returns requests the given approver can act on (excludes their own submissions).</summary>
        Function GetRequestsForApproverAsync(userId As String, approverRole As String) As Task(Of IEnumerable(Of RentalRequest))
        Function CreateRequestAsync(vm As RentalRequestViewModel, userId As String, submitterRole As String) As Task(Of RentalRequest)
        Function ApproveRequestAsync(requestId As Integer, approverUser As String, approverRole As String) As Task(Of (Success As Boolean, Message As String))
        Function RejectRequestAsync(requestId As Integer, reason As String, adminUserName As String) As Task(Of Boolean)
        Function CancelRequestAsync(requestId As Integer, userName As String) As Task(Of Boolean)
        Function GetFCFSQueueAsync() As Task(Of IEnumerable(Of RentalRequest))
        ''' <summary>Determines which ApprovalStage a request should start at given the submitter role and grand total.</summary>
        Function DetermineInitialStage(submitterRole As String, grandTotal As Decimal) As ApprovalStage
        Function GetAvailableQuantityForDatesAsync(itemId As Integer, startDate As DateTime, endDate As DateTime, excludeRequestId As Integer) As Task(Of Integer)
    End Interface

    Public Interface IInventoryService
        Function GetAllItemsAsync() As Task(Of IEnumerable(Of InventoryItem))
        Function GetItemByIdAsync(id As Integer) As Task(Of InventoryItem)
        Function GetCategoriesAsync() As Task(Of IEnumerable(Of InventoryCategory))
        Function CreateItemAsync(vm As InventoryViewModel) As Task(Of Boolean)
        Function UpdateItemAsync(vm As InventoryViewModel, updatedBy As String) As Task(Of Boolean)
        Function DeleteItemAsync(id As Integer) As Task(Of Boolean)
        Function UpdatePriceAsync(itemId As Integer, newPrice As Decimal, effectiveDate As DateTime, reason As String, updatedBy As String) As Task(Of Boolean)
        Function GetPriceHistoryAsync(itemId As Integer) As Task(Of IEnumerable(Of PriceHistory))
        Function GetAllPriceHistoriesAsync() As Task(Of IEnumerable(Of PriceHistory))
        Function GetLowStockItemsAsync() As Task(Of IEnumerable(Of InventoryItem))
        Function GetInventoryForRequestAsync() As Task(Of IEnumerable(Of InventoryItem))
        Function GetItemPriceJsonAsync() As Task(Of Dictionary(Of Integer, Decimal))
    End Interface

    Public Interface IReportService
        Function GenerateReportAsync(vm As ReportViewModel) As Task(Of ReportViewModel)
        Function ExportToCsvAsync(vm As ReportViewModel) As Task(Of Byte())
        Function ExportToExcelAsync(vm As ReportViewModel) As Task(Of Byte())
        ''' <summary>Export all RentalRequests for the specified month and year as an Excel (.xlsx) file.</summary>
        Function ExportRentalRequestsExcelAsync(month As Integer, year As Integer) As Task(Of Byte())
    End Interface

    Public Interface IAuditService
        Function LogAsync(userId As String, action As String, entityName As String, entityId As String, description As String, oldValues As String, newValues As String, ipAddress As String) As Task
        Function GetLogsAsync(page As Integer, pageSize As Integer) As Task(Of (Logs As IEnumerable(Of AuditLog), Total As Integer))
    End Interface

    Public Interface INotificationService
        Function SendNotificationAsync(userId As String, title As String, message As String, notifType As NotificationType, linkUrl As String) As Task
        Function SendToAllAdminsAsync(title As String, message As String, notifType As NotificationType, linkUrl As String) As Task
        ''' <summary>Send notification to all users in the specified role (e.g. "HOD", "GM", "SuperAdmin").</summary>
        Function SendToRoleAsync(role As String, title As String, message As String, notifType As NotificationType, linkUrl As String) As Task
        Function GetUserNotificationsAsync(userId As String) As Task(Of IEnumerable(Of Notification))
        Function GetUnreadCountAsync(userId As String) As Task(Of Integer)
        Function MarkAsReadAsync(notifId As Integer) As Task
        Function MarkAllAsReadAsync(userId As String) As Task
    End Interface
End Namespace
