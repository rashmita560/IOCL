Imports System.ComponentModel.DataAnnotations
Imports System.ComponentModel.DataAnnotations.Schema

Namespace Models.Entities
    ''' <summary>
    ''' Hall Booking – supports booking one or multiple facilities in a single request.
    ''' The BookingFacilities collection holds which facilities were booked and the
    ''' rate that was locked in at the time of booking.
    ''' </summary>
    Public Class HallBooking
        <Key>
        Public Property Id As Integer

        <ForeignKey("User")>
        Public Property UserId As String = String.Empty
        Public Property User As ApplicationUser

        <Required>
        <Display(Name:="Event Name")>
        <StringLength(200)>
        Public Property EventName As String = String.Empty

        <Display(Name:="Event Type")>
        Public Property EventType As EventType = EventType.Other

        <Required>
        <Display(Name:="Event Date")>
        Public Property EventDate As DateTime

        <Required>
        <Display(Name:="Start Time")>
        Public Property StartTime As TimeSpan

        <Required>
        <Display(Name:="End Time")>
        Public Property EndTime As TimeSpan

        <Display(Name:="Expected Attendees")>
        Public Property ExpectedAttendees As Integer

        <StringLength(500)>
        <Display(Name:="Remarks")>
        Public Property Remarks As String = String.Empty

        ''' <summary>Sum of RateAtBooking for all selected facilities × duration factor.</summary>
        <Column(TypeName:="decimal(10,2)")>
        <Display(Name:="Total Amount (₹)")>
        Public Property TotalAmount As Decimal

        <Display(Name:="Booking Mode")>
        Public Property BookingMode As BookingMode = BookingMode.SingleFacility

        <Display(Name:="Status")>
        Public Property Status As BookingStatus = BookingStatus.Pending

        <Display(Name:="Submitted At")>
        Public Property CreatedAt As DateTime = DateTime.UtcNow

        <Display(Name:="Reviewed At")>
        Public Property ReviewedAt As DateTime?

        <StringLength(50)>
        Public Property ReviewedBy As String = String.Empty

        <StringLength(500)>
        Public Property RejectionReason As String = String.Empty

        ' Navigation – one booking can cover multiple facilities
        Public Property BookingFacilities As ICollection(Of BookingFacility) = New List(Of BookingFacility)()
    End Class

    Public Enum BookingMode
        SingleFacility = 0
        Combined = 1
    End Enum

    Public Enum BookingStatus
        Pending = 0
        Approved = 1
        Rejected = 2
        Cancelled = 3
    End Enum

    Public Enum EventType
        Marriage = 0
        BirthdayParty = 1
        CulturalProgram = 2
        TownshipEvent = 3
        Meeting = 4
        SocialGathering = 5
        Other = 6
    End Enum
End Namespace
