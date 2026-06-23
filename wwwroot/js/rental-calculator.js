/* ══════════════════════════════════════════════════════════════════════════
   IOCL Panipat Township - Community Hall & Inventory Management System
   Dynamic Rental Request Live Calculator
   ══════════════════════════════════════════════════════════════════════════ */

$(document).ready(function () {
    var rowIndex = 0;

    // Add initial row on load
    addCalculatorRow();

    $('#btn-add-item').on('click', function () {
        addCalculatorRow();
    });

    // Handle item selection change
    $(document).on('change', '.item-select', function () {
        var select = $(this);
        var itemId = select.val();
        var row = select.closest('tr');
        
        if (!itemId) {
            row.find('.price-display').text('₹0.00');
            row.find('.stock-display').text('0');
            row.find('.unit-display').text('');
            row.find('.qty-input').val(0).attr('max', 0);
            row.find('.line-total-display').text('₹0.00');
            calculateGrandTotal();
            return;
        }

        // Check if this item is already selected in another row
        var duplicate = false;
        $('.item-select').not(select).each(function () {
            if ($(this).val() == itemId) {
                duplicate = true;
            }
        });

        if (duplicate) {
            alert('This item has already been added to your request. Please adjust the quantity on the existing row instead.');
            select.val('');
            row.find('.price-display').text('₹0.00');
            row.find('.stock-display').text('0');
            row.find('.unit-display').text('');
            row.find('.qty-input').val(0).attr('max', 0);
            row.find('.line-total-display').text('₹0.00');
            calculateGrandTotal();
            return;
        }

        var startDate = $('#StartDate').val();
        var endDate = $('#EndDate').val();

        $.get('/RentalRequest/GetItemPrice', { itemId: itemId, startDate: startDate, endDate: endDate }, function (response) {
            if (response.success) {
                row.find('.price-display').text('₹' + Number(response.price).toFixed(2));
                row.find('.stock-display').text(response.available);
                row.find('.unit-display').text(response.unit);
                row.find('.qty-input').attr('max', response.available);
                
                var qtyInput = row.find('.qty-input');
                var currentQty = parseInt(qtyInput.val()) || 0;
                if (currentQty > response.available) {
                    qtyInput.val(response.available);
                } else if (currentQty === 0 && response.available > 0) {
                    qtyInput.val(1);
                }
                updateLineTotal(row);
            }
        });
    });

    // Handle quantity change
    $(document).on('input change', '.qty-input', function () {
        var input = $(this);
        var val = parseInt(input.val()) || 0;
        var max = parseInt(input.attr('max')) || 0;
        
        if (val < 0) input.val(0);
        if (val > max) {
            alert('Cannot request more than available stock (' + max + ' units).');
            input.val(max);
        }
        
        updateLineTotal(input.closest('tr'));
    });

    // Handle row deletion
    $(document).on('click', '.btn-delete-row', function () {
        if ($('#itemsContainer tr').length > 1) {
            $(this).closest('tr').remove();
            reindexRows();
            calculateGrandTotal();
        } else {
            alert('At least one item is required in a rental request.');
        }
    });

    // Sync datepicker constraints dynamically
    $('#StartDate').on('change', function () {
        var startVal = $(this).val();
        if (startVal) {
            $('#EndDate').attr('min', startVal);
            var endVal = $('#EndDate').val();
            if (endVal && new Date(endVal) < new Date(startVal)) {
                $('#EndDate').val(startVal);
            }
        }
    });

    // Auto-populate StartDate and EndDate when EventDate changes
    $('#EventDate').on('change', function () {
        var eventVal = $(this).val();
        if (eventVal) {
            $('#StartDate').val(eventVal).trigger('change');
            
            var eventDate = new Date(eventVal);
            eventDate.setDate(eventDate.getDate() + 1);
            var yyyy = eventDate.getFullYear();
            var mm = String(eventDate.getMonth() + 1).padStart(2, '0');
            var dd = String(eventDate.getDate()).padStart(2, '0');
            var endVal = `${yyyy}-${mm}-${dd}`;
            $('#EndDate').val(endVal).trigger('change');
        }
    });

    // Recalculate on date changes
    $('#StartDate, #EndDate').on('change', function () {
        updateAllRowStock();
        calculateGrandTotal();
    });

    // Form submit date guard
    $('#rentalRequestForm').on('submit', function (e) {
        var startVal = $('#StartDate').val();
        var endVal = $('#EndDate').val();
        if (startVal && endVal && new Date(endVal) < new Date(startVal)) {
            alert('Item Required Until date cannot be earlier than Item Required From date.');
            e.preventDefault();
            return false;
        }
    });

    // Fetch dynamic stock for all active rows when dates change
    function updateAllRowStock() {
        var startDate = $('#StartDate').val();
        var endDate = $('#EndDate').val();
        
        $('#itemsContainer tr').each(function () {
            var row = $(this);
            var itemId = row.find('.item-select').val();
            if (itemId) {
                $.get('/RentalRequest/GetItemPrice', { itemId: itemId, startDate: startDate, endDate: endDate }, function (response) {
                    if (response.success) {
                        row.find('.stock-display').text(response.available);
                        var qtyInput = row.find('.qty-input');
                        qtyInput.attr('max', response.available);
                        
                        var currentQty = parseInt(qtyInput.val()) || 0;
                        if (currentQty > response.available) {
                            qtyInput.val(response.available);
                            updateLineTotal(row);
                        }
                    }
                });
            }
        });
    }

    // Add a row to the table
    function addCalculatorRow() {
        var optionsHtml = '<option value="">-- Select Item --</option>';
        availableItemsList.forEach(function (item) {
            optionsHtml += '<option value="' + item.Id + '">' + item.Name + '</option>';
        });

        var rowHtml = `
            <tr class="item-row" data-index="${rowIndex}">
                <td>
                    <select name="RequestItems[${rowIndex}].InventoryItemId" class="form-select iocl-form-control item-select" required>
                        ${optionsHtml}
                    </select>
                </td>
                <td>
                    <span class="price-display fw-bold text-dark">₹0.00</span>
                    <span class="unit-display small text-muted ms-1"></span>
                </td>
                <td>
                    <span class="stock-display badge bg-secondary">0</span>
                </td>
                <td>
                    <input type="number" name="RequestItems[${rowIndex}].RequestedQuantity" class="form-control iocl-form-control qty-input" value="0" min="0" required />
                </td>
                <td>
                    <span class="line-total-display fw-bold text-primary">₹0.00</span>
                </td>
                <td class="text-center">
                    <button type="button" class="btn btn-sm btn-outline-danger btn-delete-row">
                        <i class="bi bi-trash-fill"></i>
                    </button>
                </td>
            </tr>
        `;

        $('#itemsContainer').append(rowHtml);
        rowIndex++;
        calculateGrandTotal();
    }

    // Update the line total for a row
    function updateLineTotal(row) {
        var itemId = row.find('.item-select').val();
        var qty = parseInt(row.find('.qty-input').val()) || 0;
        var lineTotal = 0;

        if (itemId) {
            var item = availableItemsList.find(i => i.Id == itemId);
            if (item) {
                lineTotal = Number(item.CurrentPrice) * qty;
            }
        }

        row.find('.line-total-display').text('₹' + lineTotal.toFixed(2));
        calculateGrandTotal();
    }

    // Calculate grand total factoring in duration (number of days)
    function calculateGrandTotal() {
        var total = 0;
        
        // Sum all line totals
        $('#itemsContainer tr').each(function () {
            var row = $(this);
            var itemId = row.find('.item-select').val();
            var qty = parseInt(row.find('.qty-input').val()) || 0;
            
            if (itemId) {
                var item = availableItemsList.find(i => i.Id == itemId);
                if (item) {
                    total += Number(item.CurrentPrice) * qty;
                }
            }
        });

        // Calculate duration in days
        var startVal = $('#StartDate').val();
        var endVal = $('#EndDate').val();
        var days = 1;

        if (startVal && endVal) {
            var start = new Date(startVal);
            var end = new Date(endVal);
            var timeDiff = end.getTime() - start.getTime();
            if (timeDiff > 0) {
                days = Math.ceil(timeDiff / (1000 * 3600 * 24));
            }
        }

        var grandTotal = total * days;

        $('#grandTotalText').text('₹' + grandTotal.toFixed(2) + ' (for ' + days + ' day' + (days > 1 ? 's' : '') + ')');
        $('#inputGrandTotal').val(grandTotal.toFixed(2));
    }

    // Re-index names to keep ASP.NET MVC binding continuous after deletions
    function reindexRows() {
        rowIndex = 0;
        $('#itemsContainer tr').each(function () {
            var row = $(this);
            row.attr('data-index', rowIndex);
            row.find('.item-select').attr('name', `RequestItems[${rowIndex}].InventoryItemId`);
            row.find('.qty-input').attr('name', `RequestItems[${rowIndex}].RequestedQuantity`);
            rowIndex++;
        });
    }
});
