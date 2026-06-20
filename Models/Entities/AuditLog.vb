Imports System.ComponentModel.DataAnnotations
Imports System.ComponentModel.DataAnnotations.Schema

Namespace Models.Entities
    Public Class AuditLog
        <Key>
        Public Property Id As Integer

        <ForeignKey("User")>
        Public Property UserId As String = String.Empty
        Public Property User As ApplicationUser

        <Required>
        <StringLength(100)>
        <Display(Name:="Action")>
        Public Property Action As String = String.Empty

        <Required>
        <StringLength(100)>
        <Display(Name:="Entity")>
        Public Property EntityName As String = String.Empty

        <Display(Name:="Entity ID")>
        Public Property EntityId As String = String.Empty

        <StringLength(2000)>
        <Display(Name:="Old Values")>
        Public Property OldValues As String = String.Empty

        <StringLength(2000)>
        <Display(Name:="New Values")>
        Public Property NewValues As String = String.Empty

        <StringLength(500)>
        <Display(Name:="Description")>
        Public Property Description As String = String.Empty

        <StringLength(50)>
        <Display(Name:="IP Address")>
        Public Property IpAddress As String = String.Empty

        <Display(Name:="Timestamp")>
        Public Property CreatedAt As DateTime = DateTime.UtcNow
    End Class
End Namespace
