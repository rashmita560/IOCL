Imports System.ComponentModel.DataAnnotations
Imports System.ComponentModel.DataAnnotations.Schema

Namespace Models.Entities
    Public Class Notification
        <Key>
        Public Property Id As Integer

        <ForeignKey("User")>
        Public Property UserId As String = String.Empty
        Public Property User As ApplicationUser

        <Required>
        <StringLength(200)>
        <Display(Name:="Title")>
        Public Property Title As String = String.Empty

        <Required>
        <StringLength(1000)>
        <Display(Name:="Message")>
        Public Property Message As String = String.Empty

        <Display(Name:="Type")>
        Public Property NotificationType As NotificationType = NotificationType.Info

        <Display(Name:="Is Read")>
        Public Property IsRead As Boolean = False

        <StringLength(200)>
        <Display(Name:="Link URL")>
        Public Property LinkUrl As String = String.Empty

        <Display(Name:="Created On")>
        Public Property CreatedAt As DateTime = DateTime.UtcNow

        <Display(Name:="Read On")>
        Public Property ReadAt As DateTime?
    End Class

    Public Enum NotificationType
        Info = 0
        Success = 1
        Warning = 2
        Danger = 3
        NewRequest = 4
        Approved = 5
        Rejected = 6
        LowInventory = 7
        EventReminder = 8
    End Enum
End Namespace
