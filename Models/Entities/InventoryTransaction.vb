Imports System.ComponentModel.DataAnnotations
Imports System.ComponentModel.DataAnnotations.Schema

Namespace Models.Entities
    Public Class InventoryTransaction
        <Key>
        Public Property Id As Integer

        <ForeignKey("InventoryItem")>
        Public Property InventoryItemId As Integer
        Public Property InventoryItem As InventoryItem

        <ForeignKey("RentalRequest")>
        Public Property RentalRequestId As Integer?
        Public Property RentalRequest As RentalRequest

        <Display(Name:="Transaction Type")>
        Public Property TransactionType As TransactionType

        <Display(Name:="Quantity Changed")>
        Public Property QuantityChanged As Integer

        <Display(Name:="Quantity Before")>
        Public Property QuantityBefore As Integer

        <Display(Name:="Quantity After")>
        Public Property QuantityAfter As Integer

        <StringLength(300)>
        <Display(Name:="Notes")>
        Public Property Notes As String = String.Empty

        <StringLength(100)>
        Public Property PerformedBy As String = String.Empty

        <Display(Name:="Transaction Date")>
        Public Property CreatedAt As DateTime = DateTime.UtcNow
    End Class

    Public Enum TransactionType
        Allocation = 0       ' Reserved for approved request
        Release = 1          ' Returned after event
        StockIn = 2          ' New stock added
        StockOut = 3         ' Stock removed/damaged
        Adjustment = 4       ' Manual adjustment
    End Enum
End Namespace
