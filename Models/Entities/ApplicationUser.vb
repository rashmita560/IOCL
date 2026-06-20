Imports Microsoft.AspNetCore.Identity
Imports System.ComponentModel.DataAnnotations

Namespace Models.Entities
    ''' <summary>
    ''' ASP.NET Identity user — acts as the Authentication record.
    ''' UserName is always set to the 8-digit EmployeeId for login.
    ''' Full profile data lives in the linked Employee entity.
    ''' </summary>
    Public Class ApplicationUser
        Inherits IdentityUser

        ''' <summary>8-digit employee number — used as UserName for login.</summary>
        <StringLength(8)>
        <Display(Name:="Employee ID")>
        Public Property EmployeeId As String = String.Empty

        ''' <summary>Display name — mirrors Employee.EmployeeName for convenience.</summary>
        <StringLength(150)>
        <Display(Name:="Full Name")>
        Public Property FullName As String = String.Empty

        ''' <summary>Department — mirrors Employee.Department for convenience.</summary>
        <StringLength(150)>
        <Display(Name:="Department")>
        Public Property Department As String = String.Empty

        <Display(Name:="Is Active")>
        Public Property IsActive As Boolean = True

        <Display(Name:="Must Change Password")>
        Public Property MustChangePassword As Boolean = False

        <Display(Name:="Created On")>
        Public Property CreatedAt As DateTime = DateTime.UtcNow

        <Display(Name:="Last Login")>
        Public Property LastLoginAt As DateTime?

        ' Navigation — links to full employee profile
        Public Property Employee As Employee

        ' Navigation — transactional records
        Public Property RentalRequests As ICollection(Of RentalRequest) = New List(Of RentalRequest)()
        Public Property HallBookings As ICollection(Of HallBooking) = New List(Of HallBooking)()
        Public Property Notifications As ICollection(Of Notification) = New List(Of Notification)()
        Public Property AuditLogs As ICollection(Of AuditLog) = New List(Of AuditLog)()
    End Class
End Namespace
