Imports System.ComponentModel.DataAnnotations
Imports System.ComponentModel.DataAnnotations.Schema

Namespace Models.Entities
    ''' <summary>
    ''' Join table: records which facility/facilities were included in a booking
    ''' and the rate that was locked in at the time of booking.
    ''' </summary>
    Public Class BookingFacility
        <Key>
        Public Property Id As Integer

        <ForeignKey("HallBooking")>
        Public Property BookingId As Integer
        Public Property HallBooking As HallBooking

        <ForeignKey("Facility")>
        Public Property FacilityId As Integer
        Public Property Facility As CommunityHall

        ''' <summary>Snapshot of CommunityHall.RentalRatePerDay at time of booking.</summary>
        <Column(TypeName:="decimal(10,2)")>
        <Display(Name:="Rate at Booking (₹/day)")>
        Public Property RateAtBooking As Decimal
    End Class
End Namespace
