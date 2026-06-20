Imports System.ComponentModel.DataAnnotations
Imports System.ComponentModel.DataAnnotations.Schema

Namespace Models.Entities
    ''' <summary>
    ''' Facility Master – every bookable venue/space in the IOCL Township.
    ''' Super Admins can add new facilities (Open Ground, Conference Room, etc.)
    ''' without any code change.
    ''' </summary>
    Public Class CommunityHall
        <Key>
        Public Property Id As Integer

        <Required>
        <StringLength(150)>
        <Display(Name:="Facility Name")>
        Public Property Name As String = String.Empty

        <Display(Name:="Facility Type")>
        Public Property FacilityType As FacilityType = FacilityType.Other

        <StringLength(500)>
        <Display(Name:="Description")>
        Public Property Description As String = String.Empty

        <StringLength(200)>
        <Display(Name:="Location / Block")>
        Public Property Location As String = String.Empty

        <Display(Name:="Maximum Capacity")>
        Public Property Capacity As Integer

        <StringLength(500)>
        <Display(Name:="Amenities (comma separated)")>
        Public Property Facilities As String = String.Empty

        <StringLength(500)>
        <Display(Name:="Image Path")>
        Public Property ImagePath As String = String.Empty

        <Column(TypeName:="decimal(10,2)")>
        <Display(Name:="Rental Rate per Day (₹)")>
        Public Property RentalRatePerDay As Decimal

        <Display(Name:="Status")>
        Public Property Status As HallStatus = HallStatus.Available

        <Display(Name:="Is Active")>
        Public Property IsActive As Boolean = True

        <Display(Name:="Display Order")>
        Public Property DisplayOrder As Integer = 0

        <Display(Name:="Created On")>
        Public Property CreatedAt As DateTime = DateTime.UtcNow

        ' Navigation
        Public Property HallBookings As ICollection(Of BookingFacility) = New List(Of BookingFacility)()
    End Class

    Public Enum FacilityType
        MainHall = 0
        DiningHall = 1
        OpenGround = 2
        Garden = 3
        CulturalStage = 4
        ConferenceHall = 5
        MeetingRoom = 6
        Other = 7
    End Enum

    Public Enum HallStatus
        Available = 0
        PartiallyBooked = 1
        FullyBooked = 2
        UnderMaintenance = 3
    End Enum
End Namespace
