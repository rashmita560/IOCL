Imports System.ComponentModel.DataAnnotations
Imports System.ComponentModel.DataAnnotations.Schema

Namespace Models.Entities
    ''' <summary>
    ''' Employee master record — stores all profile information for an IOCL employee.
    ''' Linked 1-to-1 with ApplicationUser via EmployeeId.
    ''' </summary>
    Public Class Employee
        <Key>
        <StringLength(8)>
        <Display(Name:="Employee ID")>
        Public Property EmployeeId As String = String.Empty

        <Required>
        <StringLength(150)>
        <Display(Name:="Employee Name")>
        Public Property EmployeeName As String = String.Empty

        <StringLength(150)>
        <Display(Name:="Department")>
        Public Property Department As String = String.Empty

        <StringLength(150)>
        <Display(Name:="Designation")>
        Public Property Designation As String = String.Empty

        <StringLength(200)>
        <Display(Name:="Email Address")>
        Public Property Email As String = String.Empty

        <StringLength(20)>
        <Display(Name:="Phone Number")>
        Public Property PhoneNumber As String = String.Empty

        <StringLength(250)>
        <Display(Name:="Quarter / Address")>
        Public Property QuarterAddress As String = String.Empty

        <Display(Name:="Status")>
        Public Property Status As EmployeeStatus = EmployeeStatus.Active

        <Display(Name:="Created Date")>
        Public Property CreatedDate As DateTime = DateTime.UtcNow

        <Display(Name:="Last Updated")>
        Public Property UpdatedDate As DateTime = DateTime.UtcNow

        ' Navigation — back to auth record
        Public Property ApplicationUser As ApplicationUser
    End Class

    Public Enum EmployeeStatus
        Active = 0
        Inactive = 1
        Transferred = 2
        Retired = 3
    End Enum
End Namespace
