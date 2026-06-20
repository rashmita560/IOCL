Imports System.IO
Imports System.Text
Imports System.Net
Imports System.Net.Mail
Imports Microsoft.EntityFrameworkCore
Imports ClosedXML.Excel
Imports Microsoft.Extensions.Logging
Imports IOCLCommunityHall.Data
Imports IOCLCommunityHall.Models.Entities
Imports IOCLCommunityHall.Models.ViewModels

Namespace Services
    Public Class ReportService
        Implements IReportService

        Private ReadOnly _context As ApplicationDbContext

        Public Sub New(context As ApplicationDbContext)
            _context = context
        End Sub

        Public Async Function GenerateReportAsync(vm As ReportViewModel) As Task(Of ReportViewModel) Implements IReportService.GenerateReportAsync
            Dim startDate As DateTime
            Dim endDate As DateTime

            Select Case vm.ReportType
                Case ReportType.Monthly
                    startDate = New DateTime(vm.SelectedYear, vm.SelectedMonth, 1)
                    endDate = startDate.AddMonths(1).AddDays(-1)
                Case ReportType.Yearly
                    startDate = New DateTime(vm.SelectedYear, 1, 1)
                    endDate = New DateTime(vm.SelectedYear, 12, 31)
                Case ReportType.Lifetime
                    startDate = New DateTime(2020, 1, 1)
                    endDate = DateTime.Today
                Case Else ' CustomRange
                    startDate = vm.StartDate
                    endDate = vm.EndDate
            End Select

            ' Fetch approved requests in date range
            Dim requests = Await _context.RentalRequests.
                Include(Function(r) r.RentalRequestItems).
                    ThenInclude(Function(ri) ri.InventoryItem).
                        ThenInclude(Function(i) i.Category).
                Where(Function(r) r.CreatedAt >= startDate AndAlso
                                   r.CreatedAt <= endDate.AddDays(1)).
                ToListAsync()

            Dim approvedRequests = requests.Where(Function(r) r.Status = RequestStatus.Approved).ToList()

            vm.TotalRevenue = approvedRequests.Sum(Function(r) r.GrandTotal)
            vm.TotalRentals = approvedRequests.Count
            vm.ApprovedCount = approvedRequests.Count
            vm.RejectedCount = requests.Where(Function(r) r.Status = RequestStatus.Rejected).Count
            vm.PendingCount = requests.Where(Function(r) r.Status = RequestStatus.Pending).Count

            ' Hall bookings (disabled)
            vm.TotalHallBookings = 0

            ' Most used items
            vm.MostUsedItems = approvedRequests.
                SelectMany(Function(r) r.RentalRequestItems).
                GroupBy(Function(ri) New With {Key ri.InventoryItem?.Name, Key .CategoryName = ri.InventoryItem?.Category?.Name}).
                Select(Function(g) New ItemUsageReport With {
                    .ItemName = g.Key.Name,
                    .CategoryName = g.Key.CategoryName,
                    .TotalQuantityRented = g.Sum(Function(ri) ri.AllocatedQuantity),
                    .TotalRevenue = g.Sum(Function(ri) CDec(ri.AllocatedQuantity) * ri.UnitPriceAtRequest),
                    .UsageCount = g.Count()
                }).
                OrderByDescending(Function(i) i.TotalQuantityRented).
                Take(10).
                ToList()

            ' Monthly breakdown
            vm.MonthlyBreakdown = approvedRequests.
                GroupBy(Function(r) New With {Key r.CreatedAt.Year, Key r.CreatedAt.Month}).
                Select(Function(g) New MonthlyRevenueReport With {
                    .Year = g.Key.Year,
                    .Month = New DateTime(g.Key.Year, g.Key.Month, 1).ToString("MMM yyyy"),
                    .Revenue = g.Sum(Function(r) r.GrandTotal),
                    .RequestCount = g.Count()
                }).
                OrderBy(Function(m) m.Year).
                ThenBy(Function(m) m.Month).
                ToList()

            ' Hall utilization (disabled)
            vm.HallUtilization = New List(Of HallUtilizationReport)()

            Return vm
        End Function

        Public Async Function ExportToCsvAsync(vm As ReportViewModel) As Task(Of Byte()) Implements IReportService.ExportToCsvAsync
            Dim report = Await GenerateReportAsync(vm)
            Dim sb As New StringBuilder()
            sb.AppendLine("IOCL Panipat Township - Community Hall Rental Report")
            sb.AppendLine($"Report Type: {vm.ReportType}")
            sb.AppendLine($"Generated On: {DateTime.Now:dd/MM/yyyy HH:mm}")
            sb.AppendLine()
            sb.AppendLine("SUMMARY")
            sb.AppendLine($"Total Revenue (₹),{report.TotalRevenue:F2}")
            sb.AppendLine($"Total Rentals,{report.TotalRentals}")
            sb.AppendLine($"Approved,{report.ApprovedCount}")
            sb.AppendLine($"Rejected,{report.RejectedCount}")
            sb.AppendLine($"Pending,{report.PendingCount}")
            sb.AppendLine($"Hall Bookings,{report.TotalHallBookings}")
            sb.AppendLine()
            sb.AppendLine("MOST USED ITEMS")
            sb.AppendLine("Item Name,Category,Qty Rented,Revenue (₹),Usage Count")
            For Each item In report.MostUsedItems
                sb.AppendLine($"{item.ItemName},{item.CategoryName},{item.TotalQuantityRented},{item.TotalRevenue:F2},{item.UsageCount}")
            Next
            sb.AppendLine()
            sb.AppendLine("MONTHLY BREAKDOWN")
            sb.AppendLine("Month,Revenue (₹),Requests")
            For Each monthItem In report.MonthlyBreakdown
                sb.AppendLine($"{monthItem.Month},{monthItem.Revenue:F2},{monthItem.RequestCount}")
            Next
            Return Encoding.UTF8.GetBytes(sb.ToString())
        End Function

        Public Async Function ExportToExcelAsync(vm As ReportViewModel) As Task(Of Byte()) Implements IReportService.ExportToExcelAsync
            Dim report = Await GenerateReportAsync(vm)
            Using workbook As New XLWorkbook()
                ' Summary Sheet
                Dim summarySheet = workbook.Worksheets.Add("Summary")
                summarySheet.Cell(1, 1).Value = "IOCL Panipat Township - Community Hall Rental Report"
                summarySheet.Cell(1, 1).Style.Font.Bold = True
                summarySheet.Cell(1, 1).Style.Font.FontSize = 14
                summarySheet.Cell(2, 1).Value = $"Generated: {DateTime.Now:dd/MM/yyyy HH:mm}"
                summarySheet.Cell(4, 1).Value = "Metric" : summarySheet.Cell(4, 2).Value = "Value"
                summarySheet.Cell(5, 1).Value = "Total Revenue (₹)" : summarySheet.Cell(5, 2).Value = CDbl(report.TotalRevenue)
                summarySheet.Cell(6, 1).Value = "Total Rentals" : summarySheet.Cell(6, 2).Value = report.TotalRentals
                summarySheet.Cell(7, 1).Value = "Approved" : summarySheet.Cell(7, 2).Value = report.ApprovedCount
                summarySheet.Cell(8, 1).Value = "Rejected" : summarySheet.Cell(8, 2).Value = report.RejectedCount
                summarySheet.Cell(9, 1).Value = "Hall Bookings" : summarySheet.Cell(9, 2).Value = report.TotalHallBookings
                summarySheet.Columns().AdjustToContents()

                ' Most Used Items Sheet
                Dim itemSheet = workbook.Worksheets.Add("Most Used Items")
                itemSheet.Cell(1, 1).Value = "Item Name" : itemSheet.Cell(1, 2).Value = "Category"
                itemSheet.Cell(1, 3).Value = "Qty Rented" : itemSheet.Cell(1, 4).Value = "Revenue (₹)"
                Dim row = 2
                For Each item In report.MostUsedItems
                    itemSheet.Cell(row, 1).Value = item.ItemName
                    itemSheet.Cell(row, 2).Value = item.CategoryName
                    itemSheet.Cell(row, 3).Value = item.TotalQuantityRented
                    itemSheet.Cell(row, 4).Value = CDbl(item.TotalRevenue)
                    row += 1
                Next
                itemSheet.Columns().AdjustToContents()

                Using ms As New MemoryStream()
                    workbook.SaveAs(ms)
                    Return ms.ToArray()
                End Using
            End Using
        End Function
    End Class

    Public Class AuditService
        Implements IAuditService

        Private ReadOnly _context As ApplicationDbContext

        Public Sub New(context As ApplicationDbContext)
            _context = context
        End Sub

        Public Async Function LogAsync(userId As String, action As String, entityName As String, entityId As String, description As String, oldValues As String, newValues As String, ipAddress As String) As Task Implements IAuditService.LogAsync
            Await _context.AuditLogs.AddAsync(New AuditLog With {
                .UserId = userId,
                .Action = action,
                .EntityName = entityName,
                .EntityId = entityId,
                .Description = description,
                .OldValues = oldValues,
                .NewValues = newValues,
                .IpAddress = ipAddress,
                .CreatedAt = DateTime.UtcNow
            })
            Await _context.SaveChangesAsync()
        End Function

        Public Async Function GetLogsAsync(page As Integer, pageSize As Integer) As Task(Of (Logs As IEnumerable(Of AuditLog), Total As Integer)) Implements IAuditService.GetLogsAsync
            Dim query = _context.AuditLogs.Include(Function(l) l.User).OrderByDescending(Function(l) l.CreatedAt)
            Dim total = Await query.CountAsync()
            Dim logs = Await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync()
            Return (logs, total)
        End Function
    End Class

    Public Class NotificationService
        Implements INotificationService

        Private ReadOnly _context As ApplicationDbContext
        Private ReadOnly _userManager As Microsoft.AspNetCore.Identity.UserManager(Of Models.Entities.ApplicationUser)
        Private ReadOnly _config As Microsoft.Extensions.Configuration.IConfiguration
        Private ReadOnly _logger As Microsoft.Extensions.Logging.ILogger(Of NotificationService)

        Public Sub New(context As ApplicationDbContext,
                       userManager As Microsoft.AspNetCore.Identity.UserManager(Of Models.Entities.ApplicationUser),
                       config As Microsoft.Extensions.Configuration.IConfiguration,
                       logger As Microsoft.Extensions.Logging.ILogger(Of NotificationService))
            _context = context
            _userManager = userManager
            _config = config
            _logger = logger
        End Sub

        Public Async Function SendNotificationAsync(userId As String, title As String, message As String, notifType As NotificationType, linkUrl As String) As Task Implements INotificationService.SendNotificationAsync
            If String.IsNullOrEmpty(userId) Then Return

            ' 1. Save DB Notification
            Await _context.Notifications.AddAsync(New Notification With {
                .UserId = userId, .Title = title, .Message = message,
                .NotificationType = notifType, .LinkUrl = linkUrl, .CreatedAt = DateTime.UtcNow
            })
            Await _context.SaveChangesAsync()

            ' 2. Send SMTP Email Notification (Asynchronously in background)
            Try
                Dim user = Await _userManager.FindByIdAsync(userId)
                If user IsNot Nothing AndAlso Not String.IsNullOrEmpty(user.Email) Then
                    Dim userEmail = user.Email
                    Dim userFullName = user.FullName
                    Dim smtpServer = _config("SmtpSettings:Server")
                    Dim smtpPortStr = _config("SmtpSettings:Port")
                    Dim senderEmail = _config("SmtpSettings:SenderEmail")
                    Dim senderName = _config("SmtpSettings:SenderName")
                    Dim username = _config("SmtpSettings:Username")
                    Dim password = _config("SmtpSettings:Password")
                    Dim enableSslStr = _config("SmtpSettings:EnableSsl")

                    Dim emailTask = Task.Run(Async Function()
                        Try
                            If Not String.IsNullOrEmpty(smtpServer) Then
                                Dim smtpPort As Integer = 25
                                If Not String.IsNullOrEmpty(smtpPortStr) Then
                                    Integer.TryParse(smtpPortStr, smtpPort)
                                End If

                                Dim enableSsl As Boolean = False
                                If Not String.IsNullOrEmpty(enableSslStr) Then
                                    Boolean.TryParse(enableSslStr, enableSsl)
                                End If

                                Dim finalSenderEmail = If(String.IsNullOrEmpty(senderEmail), "no-reply@iocl-panipat.gov.in", senderEmail)
                                Dim finalSenderName = If(String.IsNullOrEmpty(senderName), "IOCL Community Hall", senderName)

                                Dim mail As New MailMessage()
                                mail.From = New MailAddress(finalSenderEmail, finalSenderName)
                                mail.To.Add(New MailAddress(userEmail, userFullName))
                                mail.Subject = $"IOCL Community Hall: {title}"

                                Dim bodyText = $"<h3>Hello {userFullName},</h3>" &
                                               $"<p>{message}</p>"

                                If Not String.IsNullOrEmpty(linkUrl) Then
                                    Dim baseUrl = "http://localhost:5000"
                                    bodyText &= $"<p><a href='{baseUrl}{linkUrl}' style='display:inline-block;padding:10px 20px;background-color:#ff6a00;color:white;text-decoration:none;border-radius:4px;'>View Details</a></p>"
                                End If

                                bodyText &= $"<hr/><p style='font-size:0.8em;color:#777;'>This is an automated notification from the IOCL Panipat Township Community Hall Rental System. Please do not reply directly to this email.</p>"

                                mail.Body = bodyText
                                mail.IsBodyHtml = True

                                Using client As New SmtpClient(smtpServer, smtpPort)
                                    client.EnableSsl = enableSsl
                                    If Not String.IsNullOrEmpty(username) AndAlso Not String.IsNullOrEmpty(password) Then
                                        client.Credentials = New NetworkCredential(username, password)
                                    End If
                                    Await client.SendMailAsync(mail)
                                End Using

                                _logger.LogInformation($"Successfully sent notification email in background to {userEmail} (Title: {title})")
                            End If
                        Catch ex As Exception
                            _logger.LogWarning(ex, $"Failed to send SMTP email notification in background to user email {userEmail}: {ex.Message}")
                        End Try
                    End Function)
                End If
            Catch ex As Exception
                _logger.LogWarning(ex, $"Failed to start SMTP email background task for user ID {userId}: {ex.Message}")
            End Try
        End Function

        Public Async Function SendToAllAdminsAsync(title As String, message As String, notifType As NotificationType, linkUrl As String) As Task Implements INotificationService.SendToAllAdminsAsync
            Dim superAdmins = Await _userManager.GetUsersInRoleAsync("SuperAdmin")
            For Each adminUser In superAdmins
                Await SendNotificationAsync(adminUser.Id, title, message, notifType, linkUrl)
            Next
        End Function

        Public Async Function SendToRoleAsync(role As String, title As String, message As String, notifType As NotificationType, linkUrl As String) As Task Implements INotificationService.SendToRoleAsync
            Dim usersInRole = Await _userManager.GetUsersInRoleAsync(role)
            For Each u In usersInRole
                Await SendNotificationAsync(u.Id, title, message, notifType, linkUrl)
            Next
        End Function

        Public Async Function GetUserNotificationsAsync(userId As String) As Task(Of IEnumerable(Of Notification)) Implements INotificationService.GetUserNotificationsAsync
            Return Await _context.Notifications.
                Where(Function(n) n.UserId = userId).
                OrderByDescending(Function(n) n.CreatedAt).
                Take(50).
                ToListAsync()
        End Function

        Public Async Function GetUnreadCountAsync(userId As String) As Task(Of Integer) Implements INotificationService.GetUnreadCountAsync
            Return Await _context.Notifications.CountAsync(Function(n) n.UserId = userId AndAlso Not n.IsRead)
        End Function

        Public Async Function MarkAsReadAsync(notifId As Integer) As Task Implements INotificationService.MarkAsReadAsync
            Dim notif = Await _context.Notifications.FindAsync(notifId)
            If notif IsNot Nothing Then
                notif.IsRead = True
                notif.ReadAt = DateTime.UtcNow
                Await _context.SaveChangesAsync()
            End If
        End Function

        Public Async Function MarkAllAsReadAsync(userId As String) As Task Implements INotificationService.MarkAllAsReadAsync
            Dim unread = Await _context.Notifications.Where(Function(n) n.UserId = userId AndAlso Not n.IsRead).ToListAsync()
            For Each n In unread
                n.IsRead = True
                n.ReadAt = DateTime.UtcNow
            Next
            Await _context.SaveChangesAsync()
        End Function
    End Class
End Namespace
