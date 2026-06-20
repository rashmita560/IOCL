Imports Microsoft.EntityFrameworkCore
Imports IOCLCommunityHall.Data
Imports IOCLCommunityHall.Models.Entities

Namespace Repositories
    Public Class RentalRequestRepository
        Inherits GenericRepository(Of RentalRequest)
        Implements IRentalRequestRepository

        Public Sub New(context As ApplicationDbContext)
            MyBase.New(context)
        End Sub

        Public Async Function GetRequestWithItemsAsync(id As Integer) As Task(Of RentalRequest) Implements IRentalRequestRepository.GetRequestWithItemsAsync
            Return Await _context.RentalRequests.
                Include(Function(r) r.User).
                    ThenInclude(Function(u) u.Employee).
                Include(Function(r) r.RentalRequestItems).
                    ThenInclude(Function(ri) ri.InventoryItem).
                        ThenInclude(Function(i) i.Category).
                FirstOrDefaultAsync(Function(r) r.Id = id)
        End Function

        Public Async Function GetUserRequestsAsync(userId As String) As Task(Of IEnumerable(Of RentalRequest)) Implements IRentalRequestRepository.GetUserRequestsAsync
            Return Await _context.RentalRequests.
                Include(Function(r) r.User).
                Include(Function(r) r.RentalRequestItems).
                    ThenInclude(Function(ri) ri.InventoryItem).
                Where(Function(r) r.UserId = userId).
                OrderByDescending(Function(r) r.CreatedAt).
                ToListAsync()
        End Function

        Public Async Function GetAllRequestsWithDetailsAsync() As Task(Of IEnumerable(Of RentalRequest)) Implements IRentalRequestRepository.GetAllRequestsWithDetailsAsync
            Return Await _context.RentalRequests.
                Include(Function(r) r.User).
                Include(Function(r) r.RentalRequestItems).
                    ThenInclude(Function(ri) ri.InventoryItem).
                OrderByDescending(Function(r) r.CreatedAt).
                ToListAsync()
        End Function

        ''' <summary>Returns pending requests ordered by CreatedAt ASC for FCFS processing</summary>
        Public Async Function GetPendingRequestsOrderedByFCFSAsync() As Task(Of IEnumerable(Of RentalRequest)) Implements IRentalRequestRepository.GetPendingRequestsOrderedByFCFSAsync
            Return Await _context.RentalRequests.
                Include(Function(r) r.User).
                Include(Function(r) r.RentalRequestItems).
                    ThenInclude(Function(ri) ri.InventoryItem).
                Where(Function(r) r.Status = RequestStatus.Pending).
                OrderBy(Function(r) r.CreatedAt).
                ToListAsync()
        End Function

        Public Async Function GetRequestsByStatusAsync(status As RequestStatus) As Task(Of IEnumerable(Of RentalRequest)) Implements IRentalRequestRepository.GetRequestsByStatusAsync
            Return Await _context.RentalRequests.
                Include(Function(r) r.User).
                Include(Function(r) r.RentalRequestItems).
                    ThenInclude(Function(ri) ri.InventoryItem).
                Where(Function(r) r.Status = status).
                OrderByDescending(Function(r) r.CreatedAt).
                ToListAsync()
        End Function

        Public Async Function GetRequestsByDateRangeAsync(startDate As DateTime, endDate As DateTime) As Task(Of IEnumerable(Of RentalRequest)) Implements IRentalRequestRepository.GetRequestsByDateRangeAsync
            Return Await _context.RentalRequests.
                Include(Function(r) r.User).
                Include(Function(r) r.RentalRequestItems).
                    ThenInclude(Function(ri) ri.InventoryItem).
                Where(Function(r) r.CreatedAt >= startDate AndAlso r.CreatedAt <= endDate).
                OrderByDescending(Function(r) r.CreatedAt).
                ToListAsync()
        End Function

        Public Async Function GenerateRequestNumberAsync() As Task(Of String) Implements IRentalRequestRepository.GenerateRequestNumberAsync
            Dim count = Await _context.RentalRequests.CountAsync()
            Return $"IOCL-REQ-{DateTime.Now:yyyy}-{(count + 1):D4}"
        End Function
    End Class

    Public Class InventoryRepository
        Inherits GenericRepository(Of InventoryItem)
        Implements IInventoryRepository

        Public Sub New(context As ApplicationDbContext)
            MyBase.New(context)
        End Sub

        Public Async Function GetItemWithCategoryAsync(id As Integer) As Task(Of InventoryItem) Implements IInventoryRepository.GetItemWithCategoryAsync
            Return Await _context.InventoryItems.
                Include(Function(i) i.Category).
                Include(Function(i) i.PriceHistories).
                FirstOrDefaultAsync(Function(i) i.Id = id)
        End Function

        Public Async Function GetAllWithCategoriesAsync() As Task(Of IEnumerable(Of InventoryItem)) Implements IInventoryRepository.GetAllWithCategoriesAsync
            Return Await _context.InventoryItems.
                Include(Function(i) i.Category).
                Where(Function(i) i.IsActive).
                OrderBy(Function(i) i.Category.Name).
                ThenBy(Function(i) i.Name).
                ToListAsync()
        End Function

        Public Async Function GetLowStockItemsAsync(threshold As Integer) As Task(Of IEnumerable(Of InventoryItem)) Implements IInventoryRepository.GetLowStockItemsAsync
            Return Await _context.InventoryItems.
                Include(Function(i) i.Category).
                Where(Function(i) i.IsActive AndAlso (i.TotalQuantity - i.ReservedQuantity) <= threshold).
                ToListAsync()
        End Function

        Public Async Function UpdateReservedQuantityAsync(itemId As Integer, quantityChange As Integer) As Task(Of Boolean) Implements IInventoryRepository.UpdateReservedQuantityAsync
            Dim item = Await _context.InventoryItems.FindAsync(itemId)
            If item Is Nothing Then Return False

            Dim newReserved = item.ReservedQuantity + quantityChange
            If newReserved < 0 OrElse newReserved > item.TotalQuantity Then Return False

            item.ReservedQuantity = newReserved
            item.UpdatedAt = DateTime.UtcNow
            Await _context.SaveChangesAsync()
            Return True
        End Function

        Public Async Function GetItemPriceHistoryAsync(itemId As Integer) As Task(Of IEnumerable(Of PriceHistory)) Implements IInventoryRepository.GetItemPriceHistoryAsync
            Return Await _context.PriceHistories.
                Where(Function(p) p.InventoryItemId = itemId).
                OrderByDescending(Function(p) p.EffectiveDate).
                ToListAsync()
        End Function
    End Class
End Namespace
