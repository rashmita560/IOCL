Imports System.ComponentModel.DataAnnotations
Imports IOCLCommunityHall.Models.Entities
Imports Microsoft.AspNetCore.Http

Namespace Models.ViewModels

    Public Class RentalRequestViewModel
        Public Property Id As Integer
        Public Property RequestNumber As String = String.Empty





        <Required(ErrorMessage:="Event date is required")>
        <Display(Name:="Event Date")>
        Public Property EventDate As DateTime = DateTime.Today.AddDays(1)

        <Required(ErrorMessage:="Start date is required")>
        <Display(Name:="Item Required From")>
        Public Property StartDate As DateTime = DateTime.Today.AddDays(1)

        <Required(ErrorMessage:="End date is required")>
        <Display(Name:="Item Required Until")>
        Public Property EndDate As DateTime = DateTime.Today.AddDays(2)

        <Required(ErrorMessage:="In-Principal Approval Document is required")>
        <Display(Name:="In-Principal Approval Document")>
        Public Property InPrincipalDocumentFile As IFormFile

        Public Property InPrincipalDocumentPath As String = String.Empty



        Public Property RequestItems As List(Of RentalRequestItemViewModel) = New List(Of RentalRequestItemViewModel)()
        Public Property AvailableItems As List(Of InventoryItem) = New List(Of InventoryItem)()
        Public Property GrandTotal As Decimal
        Public Property Status As RequestStatus
        Public Property CreatedAt As DateTime
        Public Property ReviewedByEmployeeId As String = String.Empty
        Public Property RejectionReason As String = String.Empty
    End Class

    Public Class RentalRequestItemViewModel
        Public Property InventoryItemId As Integer
        Public Property ItemName As String = String.Empty
        Public Property UnitType As String = String.Empty
        Public Property AvailableQuantity As Integer
        Public Property CurrentPrice As Decimal
        Public Property RequestedQuantity As Integer
        Public Property AllocatedQuantity As Integer
        Public Property LineTotal As Decimal
        Public Property ImagePath As String = String.Empty
    End Class

    Public Class InventoryViewModel
        Public Property Id As Integer

        <Required(ErrorMessage:="Item name is required")>
        <Display(Name:="Item Name")>
        Public Property Name As String = String.Empty

        <Display(Name:="Description")>
        Public Property Description As String = String.Empty

        <Required(ErrorMessage:="Category is required")>
        <Display(Name:="Category")>
        Public Property CategoryId As Integer

        <Display(Name:="Unit Type (e.g. Nos, Sets, Kg)")>
        Public Property UnitType As String = "Nos"

        <Required>
        <Range(0, 100000, ErrorMessage:="Quantity must be between 0 and 100,000")>
        <Display(Name:="Total Quantity")>
        Public Property TotalQuantity As Integer

        <Required>
        <Range(0, 999999.99, ErrorMessage:="Price must be positive")>
        <Display(Name:="Current Price (₹)")>
        Public Property CurrentPrice As Decimal

        Public Property ReservedQuantity As Integer
        Public Property IsActive As Boolean = True
        Public Property ImagePath As String = String.Empty
        Public Property ImageFile As IFormFile
        Public Property Categories As List(Of InventoryCategory) = New List(Of InventoryCategory)()
        Public Property PriceChangeReason As String = String.Empty
    End Class
End Namespace
