Imports System.IO
Imports Microsoft.EntityFrameworkCore
Imports Microsoft.Extensions.Configuration
Imports IOCLCommunityHall.Data
Imports IOCLCommunityHall.Models.Entities
Imports IOCLCommunityHall.Models.ViewModels
Imports IOCLCommunityHall.Repositories

Namespace Services
    Public Class InventoryService
        Implements IInventoryService

        Private ReadOnly _inventoryRepo As IInventoryRepository
        Private ReadOnly _context As ApplicationDbContext
        Private ReadOnly _auditService As IAuditService
        Private ReadOnly _notificationService As INotificationService
        Private ReadOnly _config As IConfiguration

        Public Sub New(inventoryRepo As IInventoryRepository,
                       context As ApplicationDbContext,
                       auditService As IAuditService,
                       notificationService As INotificationService,
                       config As IConfiguration)
            _inventoryRepo = inventoryRepo
            _context = context
            _auditService = auditService
            _notificationService = notificationService
            _config = config
        End Sub

        Public Async Function GetAllItemsAsync() As Task(Of IEnumerable(Of InventoryItem)) Implements IInventoryService.GetAllItemsAsync
            Return Await _inventoryRepo.GetAllWithCategoriesAsync()
        End Function

        Public Async Function GetItemByIdAsync(id As Integer) As Task(Of InventoryItem) Implements IInventoryService.GetItemByIdAsync
            Return Await _inventoryRepo.GetItemWithCategoryAsync(id)
        End Function

        Public Async Function GetCategoriesAsync() As Task(Of IEnumerable(Of InventoryCategory)) Implements IInventoryService.GetCategoriesAsync
            Return Await _context.InventoryCategories.OrderBy(Function(c) c.Name).ToListAsync()
        End Function

        Public Async Function CreateItemAsync(vm As InventoryViewModel) As Task(Of Boolean) Implements IInventoryService.CreateItemAsync
            Dim item = New InventoryItem With {
                .Name = vm.Name,
                .Description = vm.Description,
                .CategoryId = vm.CategoryId,
                .UnitType = vm.UnitType,
                .TotalQuantity = vm.TotalQuantity,
                .CurrentPrice = vm.CurrentPrice,
                .IsActive = vm.IsActive,
                .CreatedAt = DateTime.UtcNow,
                .UpdatedAt = DateTime.UtcNow
            }

            ' Handle image upload
            If vm.ImageFile IsNot Nothing AndAlso vm.ImageFile.Length > 0 Then
                item.ImagePath = Await SaveImageAsync(vm.ImageFile, vm.Name)
            End If

            Await _inventoryRepo.AddAsync(item)
            Await _inventoryRepo.SaveAsync()

            ' Add initial price history
            Await _context.PriceHistories.AddAsync(New PriceHistory With {
                .InventoryItemId = item.Id,
                .Year = DateTime.Now.Year,
                .PreviousPrice = 0,
                .UpdatedPrice = vm.CurrentPrice,
                .Reason = "Initial price set",
                .EffectiveDate = DateTime.Today,
                .ModifiedAt = DateTime.UtcNow
            })
            Await _context.SaveChangesAsync()
            Return True
        End Function

        Public Async Function UpdateItemAsync(vm As InventoryViewModel, updatedBy As String) As Task(Of Boolean) Implements IInventoryService.UpdateItemAsync
            Dim item = Await _inventoryRepo.GetByIdAsync(vm.Id)
            If item Is Nothing Then Return False

            Dim oldPrice = item.CurrentPrice
            Dim priceChanged = (oldPrice <> vm.CurrentPrice)

            item.Name = vm.Name
            item.Description = vm.Description
            item.CategoryId = vm.CategoryId
            item.UnitType = vm.UnitType
            item.TotalQuantity = vm.TotalQuantity
            item.CurrentPrice = vm.CurrentPrice
            item.IsActive = vm.IsActive
            item.UpdatedAt = DateTime.UtcNow

            If vm.ImageFile IsNot Nothing AndAlso vm.ImageFile.Length > 0 Then
                item.ImagePath = Await SaveImageAsync(vm.ImageFile, vm.Name)
            End If

            _inventoryRepo.Update(item)

            ' If price changed, add to price history (never deleted)
            If priceChanged Then
                Await _context.PriceHistories.AddAsync(New PriceHistory With {
                    .InventoryItemId = item.Id,
                    .Year = DateTime.Now.Year,
                    .PreviousPrice = oldPrice,
                    .UpdatedPrice = vm.CurrentPrice,
                    .Reason = If(String.IsNullOrEmpty(vm.PriceChangeReason), "Price updated by admin", vm.PriceChangeReason),
                    .EffectiveDate = DateTime.Today,
                    .ModifiedAt = DateTime.UtcNow,
                    .ModifiedBy = updatedBy
                })
            End If

            Await _inventoryRepo.SaveAsync()
            Return True
        End Function

        Public Async Function DeleteItemAsync(id As Integer) As Task(Of Boolean) Implements IInventoryService.DeleteItemAsync
            Dim item = Await _inventoryRepo.GetByIdAsync(id)
            If item Is Nothing Then Return False

            ' Soft delete — preserve history
            item.IsActive = False
            item.UpdatedAt = DateTime.UtcNow
            _inventoryRepo.Update(item)
            Await _inventoryRepo.SaveAsync()
            Return True
        End Function

        Public Async Function UpdatePriceAsync(itemId As Integer, newPrice As Decimal, effectiveDate As DateTime, reason As String, updatedBy As String) As Task(Of Boolean) Implements IInventoryService.UpdatePriceAsync
            Dim item = Await _inventoryRepo.GetByIdAsync(itemId)
            If item Is Nothing Then Return False

            Dim oldPrice = item.CurrentPrice
            item.CurrentPrice = newPrice
            item.UpdatedAt = DateTime.UtcNow
            _inventoryRepo.Update(item)

            ' Log in price history — never delete these records
            Await _context.PriceHistories.AddAsync(New PriceHistory With {
                .InventoryItemId = itemId,
                .Year = effectiveDate.Year,
                .PreviousPrice = oldPrice,
                .UpdatedPrice = newPrice,
                .Reason = reason,
                .EffectiveDate = effectiveDate,
                .ModifiedAt = DateTime.UtcNow,
                .ModifiedBy = updatedBy
            })

            Await _context.SaveChangesAsync()
            Return True
        End Function

        Public Async Function GetPriceHistoryAsync(itemId As Integer) As Task(Of IEnumerable(Of PriceHistory)) Implements IInventoryService.GetPriceHistoryAsync
            Return Await _inventoryRepo.GetItemPriceHistoryAsync(itemId)
        End Function

        Public Async Function GetAllPriceHistoriesAsync() As Task(Of IEnumerable(Of PriceHistory)) Implements IInventoryService.GetAllPriceHistoriesAsync
            Return Await _context.PriceHistories.
                Include(Function(p) p.InventoryItem).
                OrderByDescending(Function(p) p.ModifiedAt).
                ToListAsync()
        End Function

        Public Async Function GetLowStockItemsAsync() As Task(Of IEnumerable(Of InventoryItem)) Implements IInventoryService.GetLowStockItemsAsync
            Dim threshold = _config.GetValue(Of Integer)("AppSettings:LowInventoryThreshold", 10)
            Return Await _inventoryRepo.GetLowStockItemsAsync(threshold)
        End Function

        Public Async Function GetInventoryForRequestAsync() As Task(Of IEnumerable(Of InventoryItem)) Implements IInventoryService.GetInventoryForRequestAsync
            Return Await _inventoryRepo.GetAllWithCategoriesAsync()
        End Function

        Public Async Function GetItemPriceJsonAsync() As Task(Of Dictionary(Of Integer, Decimal)) Implements IInventoryService.GetItemPriceJsonAsync
            Dim items = Await _inventoryRepo.GetAllAsync()
            Return items.ToDictionary(Function(i) i.Id, Function(i) i.CurrentPrice)
        End Function

        Public Async Function GetTodayReservedAsync() As Task(Of Dictionary(Of Integer, Integer)) Implements IInventoryService.GetTodayReservedAsync
            ' Use server-side date to avoid timezone issues
            Dim today = DateTime.UtcNow.Date

            ' Half-open interval: allocation is active if StartDate <= today AND EndDate > today
            ' (matches the same logic used in GetAvailableQuantityForDatesAsync)
            Dim activeAllocations = Await _context.InventoryAllocations _
                .Where(Function(a) (a.Status = "Approved" OrElse a.Status = "Reserved") AndAlso
                                    a.StartDate.Date <= today AndAlso
                                    a.EndDate.Date > today) _
                .GroupBy(Function(a) a.InventoryItemId) _
                .Select(Function(g) New With {.ItemId = g.Key, .Reserved = g.Sum(Function(a) a.AllocatedQuantity)}) _
                .ToListAsync()

            Return activeAllocations.ToDictionary(Function(x) x.ItemId, Function(x) x.Reserved)
        End Function

        Private Async Function SaveImageAsync(file As Microsoft.AspNetCore.Http.IFormFile, itemName As String) As Task(Of String)
            Dim uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "inventory")
            Directory.CreateDirectory(uploadsFolder)
            Dim ext = Path.GetExtension(file.FileName)
            Dim fileName = $"{Guid.NewGuid()}{ext}"
            Dim filePath = Path.Combine(uploadsFolder, fileName)
            Using stream = New FileStream(filePath, FileMode.Create)
                Await file.CopyToAsync(stream)
            End Using
            Return $"/uploads/inventory/{fileName}"
        End Function
    End Class
End Namespace
