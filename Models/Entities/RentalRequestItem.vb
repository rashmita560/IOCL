Imports System.ComponentModel.DataAnnotations
Imports System.ComponentModel.DataAnnotations.Schema

Namespace Models.Entities
    Public Class RentalRequestItem
        <Key>
        Public Property Id As Integer

        <ForeignKey("RentalRequest")>
        Public Property RentalRequestId As Integer
        Public Property RentalRequest As RentalRequest

        <ForeignKey("InventoryItem")>
        <Display(Name:="Item")>
        Public Property InventoryItemId As Integer
        Public Property InventoryItem As InventoryItem

        <Display(Name:="Requested Quantity")>
        Public Property RequestedQuantity As Integer

        <Column(TypeName:="decimal(10,2)")>
        <Display(Name:="Unit Price at Time of Request (₹)")>
        Public Property UnitPriceAtRequest As Decimal

        <Column(TypeName:="decimal(12,2)")>
        <Display(Name:="Line Total (₹)")>
        <NotMapped>
        Public ReadOnly Property LineTotal As Decimal
            Get
                Return CDec(RequestedQuantity) * UnitPriceAtRequest
            End Get
        End Property

        ''' <summary>Allocated after admin approval (may be less than requested due to FCFS)</summary>
        <Display(Name:="Allocated Quantity")>
        Public Property AllocatedQuantity As Integer = 0

        <Display(Name:="Status")>
        Public Property Status As ItemRequestStatus = ItemRequestStatus.Pending

        <StringLength(200)>
        Public Property StatusReason As String = String.Empty
    End Class

    Public Enum ItemRequestStatus
        Pending = 0
        FullyAllocated = 1
        PartiallyAllocated = 2
        Rejected = 3
    End Enum
End Namespace
