Imports IOCLCommunityHall.Models.Entities

Namespace Repositories
    Public Interface IRentalRequestRepository
        Inherits IGenericRepository(Of RentalRequest)

        Function GetRequestWithItemsAsync(id As Integer) As Task(Of RentalRequest)
        Function GetUserRequestsAsync(userId As String) As Task(Of IEnumerable(Of RentalRequest))
        Function GetAllRequestsWithDetailsAsync() As Task(Of IEnumerable(Of RentalRequest))
        Function GetPendingRequestsOrderedByFCFSAsync() As Task(Of IEnumerable(Of RentalRequest))
        Function GetRequestsByStatusAsync(status As RequestStatus) As Task(Of IEnumerable(Of RentalRequest))
        Function GetRequestsByDateRangeAsync(startDate As DateTime, endDate As DateTime) As Task(Of IEnumerable(Of RentalRequest))
        Function GenerateRequestNumberAsync() As Task(Of String)
    End Interface

    Public Interface IInventoryRepository
        Inherits IGenericRepository(Of InventoryItem)

        Function GetItemWithCategoryAsync(id As Integer) As Task(Of InventoryItem)
        Function GetAllWithCategoriesAsync() As Task(Of IEnumerable(Of InventoryItem))
        Function GetLowStockItemsAsync(threshold As Integer) As Task(Of IEnumerable(Of InventoryItem))
        Function UpdateReservedQuantityAsync(itemId As Integer, quantityChange As Integer) As Task(Of Boolean)
        Function GetItemPriceHistoryAsync(itemId As Integer) As Task(Of IEnumerable(Of PriceHistory))
    End Interface
End Namespace
