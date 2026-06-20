Imports System.ComponentModel.DataAnnotations
Imports IOCLCommunityHall.Models.Entities
Imports Microsoft.AspNetCore.Http

Namespace Models.ViewModels

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
