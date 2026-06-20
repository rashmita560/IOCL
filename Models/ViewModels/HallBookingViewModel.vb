Imports System.ComponentModel.DataAnnotations
Imports IOCLCommunityHall.Models.Entities
Imports Microsoft.AspNetCore.Http

Namespace Models.ViewModels
    ''' <summary>
    ''' ViewModel for user-facing booking creation. Supports selecting
    ''' one or multiple facilities in a single booking request.
    ''' </summary>
    Public Class HallBookingViewModel
        Public Property Id As Integer

        ''' <summary>IDs of the facilities selected by the user (one or more).</summary>
        <Required(ErrorMessage:="Please select at least one facility")>
        <Display(Name:="Facilities")>
        Public Property SelectedFacilityIds As List(Of Integer) = New List(Of Integer)()

        <Required(ErrorMessage:="Event name is required")>
        <Display(Name:="Event Name")>
        Public Property EventName As String = String.Empty

        <Display(Name:="Event Type")>
        Public Property EventType As EventType = EventType.Other

        <Required(ErrorMessage:="Event date is required")>
        <Display(Name:="Event Date")>
        Public Property EventDate As DateTime = DateTime.Today.AddDays(1)

        <Required>
        <Display(Name:="Start Time")>
        Public Property StartTime As TimeSpan = TimeSpan.FromHours(9)

        <Required>
        <Display(Name:="End Time")>
        Public Property EndTime As TimeSpan = TimeSpan.FromHours(22)

        <Range(1, 5000)>
        <Display(Name:="Expected Attendees")>
        Public Property ExpectedAttendees As Integer

        <StringLength(500)>
        <Display(Name:="Remarks")>
        Public Property Remarks As String = String.Empty

        Public Property TotalAmount As Decimal
        Public Property EstimatedTotal As Decimal
        Public Property BookingMode As BookingMode = BookingMode.SingleFacility
        Public Property Status As BookingStatus
        Public Property CreatedAt As DateTime

        ''' <summary>Populated by controller for facility selector cards.</summary>
        Public Property AvailableFacilities As List(Of CommunityHall) = New List(Of CommunityHall)()
    End Class

    Public Class HallViewModel
        Public Property Id As Integer

        <Required>
        <Display(Name:="Facility Name")>
        Public Property Name As String = String.Empty

        <Display(Name:="Facility Type")>
        Public Property FacilityType As FacilityType = FacilityType.Other

        <Display(Name:="Description")>
        Public Property Description As String = String.Empty

        <Required>
        <Display(Name:="Location / Block")>
        Public Property Location As String = String.Empty

        <Required>
        <Range(1, 5000)>
        <Display(Name:="Maximum Capacity")>
        Public Property Capacity As Integer

        <Display(Name:="Amenities")>
        Public Property Facilities As String = String.Empty

        <Range(0, 9999999)>
        <Display(Name:="Rental Rate per Day (₹)")>
        Public Property RentalRatePerDay As Decimal

        <Display(Name:="Display Order")>
        Public Property DisplayOrder As Integer = 0

        Public Property Status As HallStatus = HallStatus.Available
        Public Property IsActive As Boolean = True
        Public Property ImagePath As String = String.Empty
        Public Property ImageFile As IFormFile
    End Class

    Public Class PriceViewModel
        Public Property ItemId As Integer
        Public Property ItemName As String = String.Empty
        Public Property CategoryName As String = String.Empty
        Public Property CurrentPrice As Decimal

        <Required>
        <Range(0.01, 999999.99)>
        <Display(Name:="New Price (₹)")>
        Public Property NewPrice As Decimal

        <Display(Name:="Effective Date")>
        Public Property EffectiveDate As DateTime = DateTime.Today

        <Required(ErrorMessage:="Please provide a reason for price change")>
        <Display(Name:="Reason for Change")>
        Public Property Reason As String = String.Empty

        Public Property PriceHistories As List(Of PriceHistory) = New List(Of PriceHistory)()
        Public Property AllItems As List(Of InventoryItem) = New List(Of InventoryItem)()
    End Class

    Public Class ReportViewModel
        Public Property ReportType As ReportType = ReportType.Monthly
        Public Property StartDate As DateTime = DateTime.Today.AddMonths(-1)
        Public Property EndDate As DateTime = DateTime.Today
        Public Property SelectedYear As Integer = DateTime.Today.Year
        Public Property SelectedMonth As Integer = DateTime.Today.Month

        ' Results
        Public Property TotalRevenue As Decimal
        Public Property TotalRentals As Integer
        Public Property TotalHallBookings As Integer
        Public Property ApprovedCount As Integer
        Public Property RejectedCount As Integer
        Public Property PendingCount As Integer
        Public Property MostUsedItems As List(Of ItemUsageReport) = New List(Of ItemUsageReport)()
        Public Property MonthlyBreakdown As List(Of MonthlyRevenueReport) = New List(Of MonthlyRevenueReport)()
        Public Property HallUtilization As List(Of HallUtilizationReport) = New List(Of HallUtilizationReport)()
    End Class

    Public Enum ReportType
        Monthly = 0
        Yearly = 1
        CustomRange = 2
        Lifetime = 3
    End Enum

    Public Class ItemUsageReport
        Public Property ItemName As String = String.Empty
        Public Property CategoryName As String = String.Empty
        Public Property TotalQuantityRented As Integer
        Public Property TotalRevenue As Decimal
        Public Property UsageCount As Integer
    End Class

    Public Class MonthlyRevenueReport
        Public Property Month As String = String.Empty
        Public Property Year As Integer
        Public Property Revenue As Decimal
        Public Property RequestCount As Integer
    End Class

    Public Class HallUtilizationReport
        Public Property HallName As String = String.Empty
        Public Property TotalBookings As Integer
        Public Property TotalRevenue As Decimal
        Public Property UtilizationPercentage As Double
    End Class
End Namespace
