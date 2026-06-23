Imports System.ComponentModel.DataAnnotations
Imports System.ComponentModel.DataAnnotations.Schema

Namespace Models.Entities
    Public Class RentalRequest
        <Key>
        Public Property Id As Integer

        <StringLength(20)>
        <Display(Name:="Request No.")>
        Public Property RequestNumber As String = String.Empty

        <ForeignKey("User")>
        Public Property UserId As String = String.Empty
        Public Property User As ApplicationUser

        ''' <summary>Role of the user who submitted this request (Employee, HOD, GM, SuperAdmin)</summary>
        <StringLength(50)>
        Public Property SubmittedByRole As String = "Employee"

        <Required>
        <Display(Name:="Event Date")>
        Public Property EventDate As DateTime

        <Required>
        <Display(Name:="Event Start Date")>
        Public Property StartDate As DateTime

        <Required>
        <Display(Name:="Event End Date")>
        Public Property EndDate As DateTime

        <StringLength(500)>
        <Display(Name:="In-Principal Approval Document")>
        Public Property InPrincipalDocumentPath As String = String.Empty

        <Column(TypeName:="decimal(12,2)")>
        <Display(Name:="Grand Total (₹)")>
        Public Property GrandTotal As Decimal

        <Display(Name:="Status")>
        Public Property Status As RequestStatus = RequestStatus.Pending

        ''' <summary>Current approval stage — determines who can approve next</summary>
        <Display(Name:="Approval Stage")>
        Public Property ApprovalStage As ApprovalStage = ApprovalStage.PendingHOD

        ' ── HOD Approval ──────────────────────────────────────────────────────────
        <Display(Name:="HOD Approved At")>
        Public Property HODApprovedAt As DateTime?

        <StringLength(50)>
        <Display(Name:="HOD Approved By Employee ID")>
        Public Property HODApprovedByEmployeeId As String = String.Empty

        ' ── GM Approval ───────────────────────────────────────────────────────────
        <Display(Name:="GM Approved At")>
        Public Property GMApprovedAt As DateTime?

        <StringLength(50)>
        <Display(Name:="GM Approved By Employee ID")>
        Public Property GMApprovedByEmployeeId As String = String.Empty

        ' ── HR Approval ───────────────────────────────────────────────────────────
        <Display(Name:="HR Approved At")>
        Public Property HRApprovedAt As DateTime?

        <StringLength(50)>
        <Display(Name:="HR Approved By Employee ID")>
        Public Property HRApprovedByEmployeeId As String = String.Empty

        ''' <summary>FCFS: Millisecond-precision timestamp — earlier = higher priority</summary>
        <Display(Name:="Submitted At")>
        Public Property CreatedAt As DateTime = DateTime.UtcNow

        <Display(Name:="Reviewed At")>
        Public Property ReviewedAt As DateTime?

        <StringLength(50)>
        <Display(Name:="Reviewed By Employee ID")>
        Public Property ReviewedByEmployeeId As String = String.Empty

        <StringLength(500)>
        Public Property RejectionReason As String = String.Empty

        ' Navigation
        Public Property RentalRequestItems As ICollection(Of RentalRequestItem) = New List(Of RentalRequestItem)()
        Public Property InventoryAllocations As ICollection(Of InventoryAllocation) = New List(Of InventoryAllocation)()
    End Class

    Public Enum RequestStatus
        Pending = 0
        Approved = 1
        Rejected = 2
        Cancelled = 3
        Waitlisted = 4
        Returned = 5   ' Rental period ended — stock automatically released by background service
    End Enum

    ''' <summary>
    ''' Tracks which approver role must act next on a rental request.
    ''' PendingHOD  → waiting for HOD approval
    ''' PendingGM   → waiting for GM approval (high-value or GM-required step)
    ''' PendingHR   → waiting for SuperAdmin/HR final approval
    ''' Approved    → all stages cleared, fully approved
    ''' Rejected    → rejected at any stage
    ''' </summary>
    Public Enum ApprovalStage
        PendingHOD = 0
        PendingGM = 1
        PendingHR = 2
        Approved = 3
        Rejected = 4
    End Enum
End Namespace
