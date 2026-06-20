Imports System.IO
Imports Microsoft.EntityFrameworkCore
Imports IOCLCommunityHall.Data
Imports IOCLCommunityHall.Models.Entities
Imports IOCLCommunityHall.Models.ViewModels

Namespace Services
    Public Class HallBookingService
        Implements IHallBookingService

        Private ReadOnly _context As ApplicationDbContext
        Private ReadOnly _notificationService As INotificationService

        Public Sub New(context As ApplicationDbContext, notificationService As INotificationService)
            _context = context
            _notificationService = notificationService
        End Sub

        ' ── Facility Master CRUD ─────────────────────────────────────────────────

        Public Async Function GetAllFacilitiesAsync() As Task(Of IEnumerable(Of CommunityHall)) Implements IHallBookingService.GetAllFacilitiesAsync
            Return Await _context.CommunityHalls.Where(Function(h) h.IsActive).OrderBy(Function(h) h.DisplayOrder).ToListAsync()
        End Function

        Public Async Function GetFacilityByIdAsync(id As Integer) As Task(Of CommunityHall) Implements IHallBookingService.GetFacilityByIdAsync
            Return Await _context.CommunityHalls.FindAsync(id)
        End Function

        Public Async Function CreateFacilityAsync(vm As HallViewModel) As Task(Of Boolean) Implements IHallBookingService.CreateFacilityAsync
            Dim facility = New CommunityHall With {
                .Name = vm.Name,
                .FacilityType = vm.FacilityType,
                .Description = vm.Description,
                .Location = vm.Location,
                .Capacity = vm.Capacity,
                .Facilities = vm.Facilities,
                .RentalRatePerDay = vm.RentalRatePerDay,
                .DisplayOrder = vm.DisplayOrder,
                .Status = vm.Status,
                .IsActive = vm.IsActive
            }
            If vm.ImageFile IsNot Nothing AndAlso vm.ImageFile.Length > 0 Then
                Dim uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "halls")
                Directory.CreateDirectory(uploadsFolder)
                Dim fileName = $"{Guid.NewGuid()}{Path.GetExtension(vm.ImageFile.FileName)}"
                Using stream = New FileStream(Path.Combine(uploadsFolder, fileName), FileMode.Create)
                    Await vm.ImageFile.CopyToAsync(stream)
                End Using
                facility.ImagePath = $"/uploads/halls/{fileName}"
            End If
            _context.CommunityHalls.Add(facility)
            Await _context.SaveChangesAsync()
            Return True
        End Function

        Public Async Function UpdateFacilityAsync(vm As HallViewModel) As Task(Of Boolean) Implements IHallBookingService.UpdateFacilityAsync
            Dim facility = Await _context.CommunityHalls.FindAsync(vm.Id)
            If facility Is Nothing Then Return False
            facility.Name = vm.Name
            facility.FacilityType = vm.FacilityType
            facility.Description = vm.Description
            facility.Location = vm.Location
            facility.Capacity = vm.Capacity
            facility.Facilities = vm.Facilities
            facility.RentalRatePerDay = vm.RentalRatePerDay
            facility.DisplayOrder = vm.DisplayOrder
            facility.Status = vm.Status
            facility.IsActive = vm.IsActive
            If vm.ImageFile IsNot Nothing AndAlso vm.ImageFile.Length > 0 Then
                Dim uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "halls")
                Directory.CreateDirectory(uploadsFolder)
                Dim fileName = $"{Guid.NewGuid()}{Path.GetExtension(vm.ImageFile.FileName)}"
                Using stream = New FileStream(Path.Combine(uploadsFolder, fileName), FileMode.Create)
                    Await vm.ImageFile.CopyToAsync(stream)
                End Using
                facility.ImagePath = $"/uploads/halls/{fileName}"
            End If
            Await _context.SaveChangesAsync()
            Return True
        End Function

        ' ── Booking Queries ──────────────────────────────────────────────────────

        Public Async Function GetAllBookingsAsync() As Task(Of IEnumerable(Of HallBooking)) Implements IHallBookingService.GetAllBookingsAsync
            Return Await _context.HallBookings.
                Include(Function(b) b.BookingFacilities).
                ThenInclude(Function(bf) bf.Facility).
                Include(Function(b) b.User).
                OrderByDescending(Function(b) b.EventDate).
                ToListAsync()
        End Function

        Public Async Function GetUserBookingsAsync(userId As String) As Task(Of IEnumerable(Of HallBooking)) Implements IHallBookingService.GetUserBookingsAsync
            Return Await _context.HallBookings.
                Include(Function(b) b.BookingFacilities).
                ThenInclude(Function(bf) bf.Facility).
                Where(Function(b) b.UserId = userId).
                OrderByDescending(Function(b) b.CreatedAt).
                ToListAsync()
        End Function

        ' ── Create Booking ───────────────────────────────────────────────────────

        Public Async Function CreateBookingAsync(vm As HallBookingViewModel, userId As String) As Task(Of (Success As Boolean, Message As String)) Implements IHallBookingService.CreateBookingAsync
            If vm.SelectedFacilityIds Is Nothing OrElse vm.SelectedFacilityIds.Count = 0 Then
                Return (False, "Please select at least one facility.")
            End If

            ' Check availability for ALL selected facilities
            Dim availResult = Await AreFacilitiesAvailableAsync(vm.SelectedFacilityIds, vm.EventDate, vm.StartTime, vm.EndTime)
            If Not availResult.Available Then
                Return (False, $"The {availResult.ConflictingFacility} is not available for the selected date and time slot.")
            End If

            ' Load all selected facilities
            Dim facilities = Await _context.CommunityHalls.
                Where(Function(h) vm.SelectedFacilityIds.Contains(h.Id)).
                ToListAsync()

            If facilities.Count = 0 Then
                Return (False, "Selected facilities not found.")
            End If

            Dim totalAmount = facilities.Sum(Function(f) f.RentalRatePerDay)
            Dim mode = If(facilities.Count > 1, BookingMode.Combined, BookingMode.SingleFacility)

            Dim booking = New HallBooking With {
                .UserId = userId,
                .EventName = vm.EventName,
                .EventType = vm.EventType,
                .EventDate = vm.EventDate,
                .StartTime = vm.StartTime,
                .EndTime = vm.EndTime,
                .ExpectedAttendees = vm.ExpectedAttendees,
                .Remarks = vm.Remarks,
                .TotalAmount = totalAmount,
                .BookingMode = mode,
                .Status = BookingStatus.Pending,
                .CreatedAt = DateTime.UtcNow
            }
            _context.HallBookings.Add(booking)
            Await _context.SaveChangesAsync()

            ' Add BookingFacility join rows
            For Each f In facilities
                _context.BookingFacilities.Add(New BookingFacility With {
                    .BookingId = booking.Id,
                    .FacilityId = f.Id,
                    .RateAtBooking = f.RentalRatePerDay
                })
            Next
            Await _context.SaveChangesAsync()

            Dim facilityNames = String.Join(" + ", facilities.Select(Function(f) f.Name))
            Await _notificationService.SendToAllAdminsAsync(
                "New Hall Booking Request",
                $"Hall booking request for {facilityNames} on {vm.EventDate:dd MMM yyyy}",
                NotificationType.NewRequest,
                "/AdminRequest/HallBookings")

            Return (True, "Hall booking request submitted successfully.")
        End Function

        ' ── Approve / Reject ─────────────────────────────────────────────────────

        Public Async Function ApproveBookingAsync(bookingId As Integer, adminUserName As String) As Task(Of Boolean) Implements IHallBookingService.ApproveBookingAsync
            Dim booking = Await _context.HallBookings.
                Include(Function(b) b.BookingFacilities).ThenInclude(Function(bf) bf.Facility).
                FirstOrDefaultAsync(Function(b) b.Id = bookingId)
            If booking Is Nothing OrElse booking.Status <> BookingStatus.Pending Then Return False

            booking.Status = BookingStatus.Approved
            booking.ReviewedAt = DateTime.UtcNow
            booking.ReviewedBy = adminUserName
            Await _context.SaveChangesAsync()

            Dim facilityNames = String.Join(" & ", booking.BookingFacilities.Select(Function(bf) bf.Facility?.Name))
            Await _notificationService.SendNotificationAsync(booking.UserId, "Hall Booking Approved",
                $"Your booking for {facilityNames} on {booking.EventDate:dd MMM yyyy} is approved.",
                NotificationType.Approved, "/HallBooking/Index")
            Return True
        End Function

        Public Async Function RejectBookingAsync(bookingId As Integer, reason As String, adminUserName As String) As Task(Of Boolean) Implements IHallBookingService.RejectBookingAsync
            Dim booking = Await _context.HallBookings.
                Include(Function(b) b.BookingFacilities).ThenInclude(Function(bf) bf.Facility).
                FirstOrDefaultAsync(Function(b) b.Id = bookingId)
            If booking Is Nothing OrElse booking.Status <> BookingStatus.Pending Then Return False

            booking.Status = BookingStatus.Rejected
            booking.ReviewedAt = DateTime.UtcNow
            booking.ReviewedBy = adminUserName
            booking.RejectionReason = reason
            Await _context.SaveChangesAsync()

            Dim facilityNames = String.Join(" & ", booking.BookingFacilities.Select(Function(bf) bf.Facility?.Name))
            Await _notificationService.SendNotificationAsync(booking.UserId, "Hall Booking Rejected",
                $"Your booking for {facilityNames} was rejected. Reason: {reason}",
                NotificationType.Rejected, "/HallBooking/Index")
            Return True
        End Function

        ' ── Availability Check (multi-facility) ──────────────────────────────────

        Public Async Function AreFacilitiesAvailableAsync(facilityIds As List(Of Integer), [date] As DateTime, startTime As TimeSpan, endTime As TimeSpan) As Task(Of (Available As Boolean, ConflictingFacility As String)) Implements IHallBookingService.AreFacilitiesAvailableAsync
            Dim targetDate = [date].Date
            For Each fid In facilityIds
                Dim targetFid = fid
                Dim bookingsOnDate = Await _context.BookingFacilities.
                    Include(Function(bf) bf.HallBooking).
                    Where(Function(bf) bf.FacilityId = targetFid AndAlso
                                       bf.HallBooking.EventDate = targetDate).
                    ToListAsync()

                Dim hasConflict = bookingsOnDate.Any(Function(bf)
                    Return bf.HallBooking.Status <> BookingStatus.Rejected AndAlso
                           bf.HallBooking.Status <> BookingStatus.Cancelled AndAlso
                           Not (endTime <= bf.HallBooking.StartTime OrElse startTime >= bf.HallBooking.EndTime)
                End Function)

                If hasConflict Then
                    Dim facilityName = Await _context.CommunityHalls.
                        Where(Function(h) h.Id = targetFid).
                        Select(Function(h) h.Name).FirstOrDefaultAsync()
                    Return (False, If(facilityName, "Selected facility"))
                End If
            Next
            Return (True, String.Empty)
        End Function

        ' ── Calendar Events ──────────────────────────────────────────────────────

        Public Async Function GetCalendarEventsAsync() As Task(Of IEnumerable(Of Object)) Implements IHallBookingService.GetCalendarEventsAsync
            Dim allBookings = Await _context.HallBookings.
                Include(Function(b) b.BookingFacilities).ThenInclude(Function(bf) bf.Facility).
                Include(Function(b) b.User).
                ToListAsync()

            Dim bookings = allBookings.Where(Function(b) b.Status <> BookingStatus.Cancelled).ToList()

            Return bookings.Select(Function(b)
                Dim facilityNames = String.Join(" + ", b.BookingFacilities.Select(Function(bf) bf.Facility?.Name))
                Return CType(New With {
                    .id = b.Id,
                    .title = $"{b.EventName} – {facilityNames}",
                    .start = b.EventDate.ToString("yyyy-MM-dd") & "T" & b.StartTime.ToString(),
                    .end = b.EventDate.ToString("yyyy-MM-dd") & "T" & b.EndTime.ToString(),
                    .color = If(b.Status = BookingStatus.Approved, "#28a745",
                                If(b.Status = BookingStatus.Pending, "#fd7e14", "#dc3545")),
                    .extendedProps = New With {
                        .facilities = facilityNames,
                        .user = b.User?.FullName,
                        .status = b.Status.ToString()
                    }
                }, Object)
            End Function)
        End Function
    End Class
End Namespace
