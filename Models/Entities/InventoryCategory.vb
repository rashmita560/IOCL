Imports System.ComponentModel.DataAnnotations

Namespace Models.Entities
    Public Class InventoryCategory
        <Key>
        Public Property Id As Integer

        <Required>
        <StringLength(100)>
        <Display(Name:="Category Name")>
        Public Property Name As String = String.Empty

        <StringLength(300)>
        <Display(Name:="Description")>
        Public Property Description As String = String.Empty

        <Display(Name:="Created On")>
        Public Property CreatedAt As DateTime = DateTime.UtcNow

        ' Navigation
        Public Property InventoryItems As ICollection(Of InventoryItem) = New List(Of InventoryItem)()
    End Class
End Namespace
