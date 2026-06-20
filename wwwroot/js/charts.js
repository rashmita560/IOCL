/* ══════════════════════════════════════════════════════════════════════════
   IOCL Panipat Township - Community Hall & Inventory Management System
   Dashboard Charts Renderer (Chart.js Configuration)
   ══════════════════════════════════════════════════════════════════════════ */

$(document).ready(function () {
    // Colors
    var ioclOrange = '#FF6600';
    var ioclBlue = '#003399';
    var charcoal = '#2B303A';
    var grayBorder = '#DEE2E6';

    // ─── 1. Monthly Revenue Chart (Line Chart) ────────────────────────────
    if ($('#revenueChart').length) {
        var ctx1 = document.getElementById('revenueChart').getContext('2d');
        new Chart(ctx1, {
            type: 'line',
            data: {
                labels: monthlyRevenueLabels,
                datasets: [{
                    label: 'Revenue (₹)',
                    data: monthlyRevenueData,
                    borderColor: ioclOrange,
                    backgroundColor: 'rgba(255, 102, 0, 0.05)',
                    borderWidth: 3,
                    fill: true,
                    tension: 0.35,
                    pointBackgroundColor: ioclOrange,
                    pointHoverRadius: 6
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: { display: false }
                },
                scales: {
                    y: {
                        beginAtZero: true,
                        grid: { color: grayBorder },
                        ticks: {
                            callback: function(value) { return '₹' + value.toLocaleString(); }
                        }
                    },
                    x: { grid: { display: false } }
                }
            }
        });
    }

    // ─── 2. Bookings Volumetric Trend (Bar Chart) ─────────────────────────
    if ($('#bookingsTrendChart').length) {
        var ctx2 = document.getElementById('bookingsTrendChart').getContext('2d');
        new Chart(ctx2, {
            type: 'bar',
            data: {
                labels: bookingTrendLabels,
                datasets: [{
                    label: 'Requests Count',
                    data: bookingTrendData,
                    backgroundColor: ioclBlue,
                    borderRadius: 5,
                    barPercentage: 0.6
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: false,
                plugins: {
                    legend: { display: false }
                },
                scales: {
                    y: {
                        beginAtZero: true,
                        grid: { color: grayBorder },
                        ticks: { stepSize: 1 }
                    },
                    x: { grid: { display: false } }
                }
            }
        });
    }

    // ─── 3. Top 5 Most Used Items (Doughnut Chart) ────────────────────────
    if ($('#topItemsChart').length) {
        var ctx3 = document.getElementById('topItemsChart').getContext('2d');
        new Chart(ctx3, {
            type: 'doughnut',
            data: {
                labels: topItemsLabels.length ? topItemsLabels : ['No usage recorded'],
                datasets: [{
                    data: topItemsData.length ? topItemsData : [1],
                    backgroundColor: [
                        '#FF6600', // Orange
                        '#003399', // Blue
                        '#0DCAF0', // Info
                        '#198754', // Success
                        '#FFC107', // Warning
                        '#DEE2E6'  // Gray (fallback)
                    ],
                    borderWidth: 2
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: true,
                plugins: {
                    legend: {
                        position: 'bottom',
                        labels: { boxWidth: 12, font: { size: 10 } }
                    }
                }
            }
        });
    }

    // ─── 4. Inventory Allocation (Doughnut Chart) ─────────────────────────
    if ($('#invAllocationChart').length) {
        var ctx4 = document.getElementById('invAllocationChart').getContext('2d');
        new Chart(ctx4, {
            type: 'doughnut',
            data: {
                labels: invStatusLabels,
                datasets: [{
                    data: invStatusData,
                    backgroundColor: ['#198754', '#FFC107'], // Success (Free) and Warning (Reserved)
                    borderWidth: 2
                }]
            },
            options: {
                responsive: true,
                maintainAspectRatio: true,
                plugins: {
                    legend: {
                        position: 'bottom',
                        labels: { boxWidth: 12, font: { size: 10 } }
                    }
                }
            }
        });
    }
});
