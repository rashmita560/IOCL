/* ══════════════════════════════════════════════════════════════════════════
   IOCL Panipat Township - Community Hall & Inventory Management System
   Common JavaScript Utilities
   ══════════════════════════════════════════════════════════════════════════ */

$(document).ready(function () {
    // ─── Sidebar Toggle & Collapse ────────────────────────────────────────
    // Hamburger menu button in header clicked
    $('#sidebarToggle').on('click', function (e) {
        e.preventDefault();
        if ($(window).width() > 768) {
            $('body').removeClass('sidebar-hidden');
        } else {
            $('body').addClass('sidebar-open');
        }
    });

    // Collapse/hide button inside sidebar clicked
    $(document).on('click', '.sidebar-collapse-btn', function (e) {
        e.preventDefault();
        if ($(window).width() > 768) {
            $('body').addClass('sidebar-hidden');
        } else {
            $('body').removeClass('sidebar-open');
        }
    });

    // Close sidebar on mobile/tablet screen if clicking outside the sidebar area
    $(document).on('click', function (e) {
        if ($(window).width() <= 768) {
            if (!$(e.target).closest('#ioclSidebar').length && 
                !$(e.target).closest('#sidebarToggle').length && 
                $('body').hasClass('sidebar-open')) {
                $('body').removeClass('sidebar-open');
            }
        }
    });

    // ─── Auto-dismiss Toast Alerts ────────────────────────────────────────
    setTimeout(function () {
        $('.alert.alert-dismissible').fadeOut('slow', function () {
            $(this).remove();
        });
    }, 5000);

    // ─── Notification Badge Update ────────────────────────────────────────
    function updateUnreadNotificationCount() {
        if ($('#notifBellBtn').length) {
            $.ajax({
                url: '/Notification/GetUnreadCount',
                type: 'GET',
                dataType: 'json',
                success: function (response) {
                    var badge = $('#notifBellBtn .notification-badge');
                    if (response.count > 0) {
                        var text = response.count > 99 ? '99+' : response.count;
                        if (badge.length) {
                            badge.text(text);
                        } else {
                            $('#notifBellBtn').append('<span class="notification-badge">' + text + '</span>');
                        }
                    } else {
                        badge.remove();
                    }
                }
            });
        }
    }

    // Run on load and poll every 60 seconds
    updateUnreadNotificationCount();
    setInterval(updateUnreadNotificationCount, 60000);

    // Mark single notification read & handle click navigation
    $(document).on('click', '.notif-mark-read', function (e) {
        e.preventDefault();
        var $el = $(this);
        var id = $el.data('id');
        var row = $el.closest('.notif-item');
        var href = $el.attr('href');

        $.ajax({
            url: '/Notification/MarkRead/' + id,
            type: 'POST',
            success: function () {
                row.removeClass('unread');
                updateUnreadNotificationCount();
                if (href && href !== '#' && href !== 'javascript:void(0);') {
                    window.location.href = href;
                }
            },
            error: function () {
                // If the AJAX fails, still redirect the user to preserve navigation functionality
                if (href && href !== '#' && href !== 'javascript:void(0);') {
                    window.location.href = href;
                }
            }
        });
    });
});
