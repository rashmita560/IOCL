Imports IOCLCommunityHall.Models.Entities

Namespace Models.ViewModels
    Public Class DashboardViewModel
        ' ── KPI Cards ───────────────────────────────────────────────────────────
        Public Property TotalRequests As Integer
        Public Property PendingRequests As Integer
        Public Property ApprovedRequests As Integer
        Public Property RejectedRequests As Integer
        Public Property TotalRevenue As Decimal
        Public Property MonthlyRevenue As Decimal
        Public Property TotalInventoryItems As Integer
        Public Property LowStockItemCount As Integer

        ' ── Charts Data ─────────────────────────────────────────────────────────
        Public Property MonthlyRevenueLabels As List(Of String) = New List(Of String)()
        Public Property MonthlyRevenueData As List(Of Decimal) = New List(Of Decimal)()
        Public Property BookingTrendLabels As List(Of String) = New List(Of String)()
        Public Property BookingTrendData As List(Of Integer) = New List(Of Integer)()
        Public Property TopItemNames As List(Of String) = New List(Of String)()
        Public Property TopItemUsage As List(Of Integer) = New List(Of Integer)()
        Public Property InventoryStatusLabels As List(Of String) = New List(Of String)()
        Public Property InventoryStatusData As List(Of Integer) = New List(Of Integer)()

        ' ── Recent Activity ─────────────────────────────────────────────────────
        Public Property RecentRequests As List(Of RentalRequest) = New List(Of RentalRequest)()
        Public Property LowStockItems As List(Of InventoryItem) = New List(Of InventoryItem)()

        ' ── User Dashboard ──────────────────────────────────────────────────────
        Public Property AvailableInventory As List(Of InventoryItem) = New List(Of InventoryItem)()
        Public Property UserRequests As List(Of RentalRequest) = New List(Of RentalRequest)()
    End Class
End Namespace
