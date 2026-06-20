Imports System.ComponentModel.DataAnnotations
Imports System.ComponentModel.DataAnnotations.Schema

Namespace Models.Entities
    ''' <summary>
    ''' Stores complete price audit trail. Records are NEVER deleted.
    ''' Table 1 = Current price is stored in InventoryItem.CurrentPrice
    ''' Table 2 = All historical price changes are recorded here
    ''' </summary>
    Public Class PriceHistory
        <Key>
        Public Property Id As Integer

        <ForeignKey("InventoryItem")>
        <Display(Name:="Item")>
        Public Property InventoryItemId As Integer
        Public Property InventoryItem As InventoryItem

        <Display(Name:="Year")>
        Public Property Year As Integer

        <Column(TypeName:="decimal(10,2)")>
        <Display(Name:="Previous Price (₹)")>
        Public Property PreviousPrice As Decimal

        <Column(TypeName:="decimal(10,2)")>
        <Display(Name:="Updated Price (₹)")>
        Public Property UpdatedPrice As Decimal

        <Display(Name:="Price Difference (₹)")>
        <NotMapped>
        Public ReadOnly Property PriceDifference As Decimal
            Get
                Return UpdatedPrice - PreviousPrice
            End Get
        End Property

        <Display(Name:="% Change")>
        <NotMapped>
        Public ReadOnly Property PercentageChange As Decimal
            Get
                If PreviousPrice = 0 Then Return 0
                Return Math.Round(((UpdatedPrice - PreviousPrice) / PreviousPrice) * 100, 2)
            End Get
        End Property

        <StringLength(300)>
        <Display(Name:="Reason for Change")>
        Public Property Reason As String = String.Empty

        <Display(Name:="Effective Date")>
        Public Property EffectiveDate As DateTime = DateTime.UtcNow

        <Display(Name:="Modified On")>
        Public Property ModifiedAt As DateTime = DateTime.UtcNow

        <StringLength(100)>
        <Display(Name:="Modified By")>
        Public Property ModifiedBy As String = String.Empty
    End Class
End Namespace
