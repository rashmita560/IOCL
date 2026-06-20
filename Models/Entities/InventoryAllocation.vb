Imports System.ComponentModel.DataAnnotations
Imports System.ComponentModel.DataAnnotations.Schema

Namespace Models.Entities
    Public Class InventoryAllocation
        <Key>
        Public Property AllocationId As Integer

        Public Property RequestId As Integer

        <ForeignKey("RequestId")>
        Public Property RentalRequest As RentalRequest

        Public Property InventoryItemId As Integer

        <ForeignKey("InventoryItemId")>
        Public Property InventoryItem As InventoryItem

        Public Property AllocatedQuantity As Integer

        Public Property StartDate As DateTime

        Public Property EndDate As DateTime

        <StringLength(50)>
        Public Property Status As String = "Approved"

        Public Property AllocationDate As DateTime? = DateTime.UtcNow
    End Class
End Namespace
