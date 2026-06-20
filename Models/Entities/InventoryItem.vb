Imports System.ComponentModel.DataAnnotations
Imports System.ComponentModel.DataAnnotations.Schema

Namespace Models.Entities
    Public Class InventoryItem
        <Key>
        Public Property Id As Integer

        <Required>
        <StringLength(150)>
        <Display(Name:="Item Name")>
        Public Property Name As String = String.Empty

        <StringLength(500)>
        <Display(Name:="Description")>
        Public Property Description As String = String.Empty

        <ForeignKey("Category")>
        <Display(Name:="Category")>
        Public Property CategoryId As Integer
        Public Property Category As InventoryCategory

        <StringLength(30)>
        <Display(Name:="Unit Type")>
        Public Property UnitType As String = "Nos"

        <Display(Name:="Total Quantity")>
        Public Property TotalQuantity As Integer

        <Display(Name:="Reserved Quantity")>
        Public Property ReservedQuantity As Integer = 0

        <Display(Name:="Available Quantity")>
        <NotMapped>
        Public ReadOnly Property AvailableQuantity As Integer
            Get
                Return TotalQuantity - ReservedQuantity
            End Get
        End Property

        <Column(TypeName:="decimal(10,2)")>
        <Display(Name:="Current Price (₹)")>
        Public Property CurrentPrice As Decimal

        <StringLength(500)>
        <Display(Name:="Image Path")>
        Public Property ImagePath As String = String.Empty

        <Display(Name:="Is Active")>
        Public Property IsActive As Boolean = True

        <Display(Name:="Created On")>
        Public Property CreatedAt As DateTime = DateTime.UtcNow

        <Display(Name:="Last Updated")>
        Public Property UpdatedAt As DateTime = DateTime.UtcNow

        ' Navigation
        Public Property RentalRequestItems As ICollection(Of RentalRequestItem) = New List(Of RentalRequestItem)()
        Public Property PriceHistories As ICollection(Of PriceHistory) = New List(Of PriceHistory)()
        Public Property InventoryTransactions As ICollection(Of InventoryTransaction) = New List(Of InventoryTransaction)()
    End Class
End Namespace
