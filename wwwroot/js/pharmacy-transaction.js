/**
 * Shared JS for Pharmacy Transactions (Purchases & Sales)
 * Handles row addition, removal, reindexing, and calculating.
 */

// Global clean number formatting utility (Overrides view-level defaults)
window.formatNumber = function(val, digits) {
    var num = parseFloat(val);
    if (isNaN(num)) return '0';
    return num.toLocaleString('en-US', { 
        minimumFractionDigits: 0, 
        maximumFractionDigits: (digits !== undefined) ? digits : 2 
    });
};

(function (window, $) {
    var PharmacyTransaction = (function () {
        var config = {
            mode: 'purchase', // 'purchase' or 'sale'
            tableBodyId: '#linesBody',
            rowTemplateId: '#rowTemplate',
            emptyContainerId: '#emptyContainer',
            formId: '#transactionForm',
            subTotalId: '#subTotalDisplay',
            grandTotalId: '#grandTotalDisplay',
            lineCount: 0,
            callbacks: {
                onRowAdded: null,
                onRowRemoved: null,
                calculateLineTotal: null,
                onBeforeReindex: null
            }
        };

        function init(options) {
            $.extend(true, config, options);

            $(config.tableBodyId).on('click', '.btn-remove-line', function () {
                removeRow($(this).closest('.erp-line-row'));
            });

            // Event delegation for recalculations
            $(config.tableBodyId).on('input change', 'input[type="number"], select', function () {
                var $input = $(this);
                var $row = $input.closest('.erp-line-row');
                
                // SweetAlert Validation for Quantity
                if ($input.hasClass('pos-qty-input')) {
                    let val = parseFloat($input.val());
                    let max = parseFloat($input.attr('data-max'));

                    // Check for negative or zero
                    if (isNaN(val) || val <= 0) {
                        if (typeof Swal !== 'undefined') {
                            Swal.fire({
                                icon: 'warning',
                                title: 'إدخال غير صالح',
                                text: 'يجب أن تكون الكمية أكبر من صفر.',
                                confirmButtonText: 'حسنا',
                                confirmButtonColor: '#0d6efd'
                            });
                        }
                        $input.val(1);
                        recalcLine($row);
                        return;
                    }

                    // Check for exceeding stock limit
                    if (!isNaN(max) && val > max) {
                        if (typeof Swal !== 'undefined') {
                            Swal.fire({
                                icon: 'error',
                                title: 'تجاوز المخزون',
                                text: `الكمية المطلوبة أكبر من المخزون المتاح (${max}).`,
                                confirmButtonText: 'حسنا',
                                confirmButtonColor: '#0d6efd'
                            });
                        }
                        $input.val(max);
                        recalcLine($row);
                        return;
                    }
                }

                // SweetAlert Validation for Price
                if ($input.hasClass('pos-price-input')) {
                    let val = parseFloat($input.val());
                    if (isNaN(val) || val <= 0) {
                        if (typeof Swal !== 'undefined') {
                            Swal.fire({
                                icon: 'warning',
                                title: 'تنبيه السعر',
                                text: 'يجب تحديد سعر المنتج لإتمام العملية.',
                                confirmButtonText: 'حسنا',
                                confirmButtonColor: '#0d6efd'
                            });
                        }
                        $input.val('0');
                        recalcLine($row);
                        return;
                    }
                }

                recalcLine($row);
            });

            $(config.formId).on('submit', function (e) {
                reindexRows();
                refreshValidation();

                if ($(config.tableBodyId).find('.erp-line-row').length === 0) {
                    if (config.emptyValidationMessage) {
                        $('#linesClientValidation').text(config.emptyValidationMessage);
                    }
                    e.preventDefault();
                    return false;
                }

                if (!$(config.formId).valid()) {
                    e.preventDefault();
                    focusFirstInvalid();
                    return false;
                }
                return true;
            });

            // Initial setup
            config.lineCount = $(config.tableBodyId).find('.erp-line-row').length;
            recalculateAll();
            toggleEmptyState();
            refreshValidation();
        }

        function addRow(prefillData) {
            var templateHtml = $(config.rowTemplateId).html();
            var timestamp = new Date().getTime() + Math.floor(Math.random() * 1000);
            var idx = timestamp;

            var rowHtml = templateHtml.replace(/\{idx\}/g, idx);
            var $newRow = $(rowHtml);
            $newRow.attr('id', 'line_row_manual_' + timestamp);
            $newRow.attr('data-line-index', idx);
            
            if (prefillData && prefillData.barcode) {
                $newRow.attr('data-barcode', prefillData.barcode);
                $newRow.find('[data-field="Barcode"]').val(prefillData.barcode);
            }

            $(config.tableBodyId).prepend($newRow);
            toggleEmptyState();

            if (config.callbacks.onRowAdded) {
                config.callbacks.onRowAdded($newRow, prefillData, idx);
            }

            refreshValidation();
            return $newRow;
        }

        function getActiveRow() {
            var $rows = $(config.tableBodyId).find('.erp-line-row');
            if ($rows.length === 0) return null;

            var $empty = $rows.filter(function() {
                var itemVal = $(this).find('.item-select').val();
                var barcodeVal = $(this).attr('data-barcode') || $(this).find('[data-field="Barcode"]').val();
                var hasItem = itemVal && itemVal !== "0" && itemVal !== "";
                var hasBarcode = barcodeVal && barcodeVal.trim() !== "";
                return !hasItem && !hasBarcode;
            }).first();

            return $empty.length ? $empty : null;
        }

        function removeRow($row) {
            $row.fadeOut(150, function () {
                $row.remove();
                recalculateAll();
                toggleEmptyState();
                refreshValidation();
                if (config.callbacks.onRowRemoved) {
                    config.callbacks.onRowRemoved();
                }
            });
        }

        function recalcLine($row) {
            if (config.callbacks.calculateLineTotal) {
                config.callbacks.calculateLineTotal($row);
            }
            recalcGrandTotal();
        }

        function recalculateAll() {
            $(config.tableBodyId).find('.erp-line-row').each(function () {
                if (config.callbacks.calculateLineTotal) {
                    config.callbacks.calculateLineTotal($(this));
                }
            });
            recalcGrandTotal();
        }

        function recalcGrandTotal() {
            var total = 0;
            $(config.tableBodyId).find('.line-est-total, .line-total-display').each(function () {
                var val = parseFloat($(this).text().replace(/,/g, ''));
                if (!isNaN(val)) total += val;
            });

            var displayVal = formatNumber(total);
            if ($(config.subTotalId).length) $(config.subTotalId).text(displayVal);
            if ($(config.grandTotalId).length) $(config.grandTotalId).text(displayVal);
        }

        function reindexRows() {
            if (config.callbacks.onBeforeReindex) {
                config.callbacks.onBeforeReindex();
            }

            $(config.tableBodyId).find('.erp-line-row').each(function (index) {
                var $row = $(this);
                $row.attr('id', 'line_row_' + index);
                $row.attr('data-line-index', index);

                $row.find('[data-field]').each(function () {
                    var $field = $(this);
                    var fieldName = $field.data('field');
                    if (fieldName) {
                        $field.attr('name', 'Lines[' + index + '].' + fieldName);
                        if ($field.attr('id')) {
                            $field.attr('id', 'Lines_' + index + '__' + fieldName);
                        }
                    }
                });

                $row.find('[data-valmsg-field]').each(function () {
                    var $msg = $(this);
                    var fieldName = $msg.attr('data-valmsg-field');
                    if (fieldName) {
                        $msg.attr('data-valmsg-for', 'Lines[' + index + '].' + fieldName);
                    }
                });
            });

            config.lineCount = $(config.tableBodyId).find('.erp-line-row').length;
        }

        function refreshValidation() {
            if (!$.validator || !$.validator.unobtrusive) return;
            var $form = $(config.formId);
            $form.removeData('validator');
            $form.removeData('unobtrusiveValidation');
            $.validator.unobtrusive.parse($form);

            var validator = $form.data('validator');
            if (validator) {
                validator.settings.ignore = ':hidden:not(.select2-hidden-accessible)';
            }
        }

        function focusFirstInvalid() {
            var $invalid = $(config.formId).find('.input-validation-error, [aria-invalid="true"]').first();
            if ($invalid.length) {
                if ($invalid.hasClass('select2-hidden-accessible')) {
                    $invalid.select2('open');
                } else {
                    $invalid.trigger('focus');
                }
            }
        }

        function toggleEmptyState() {
            if ($(config.tableBodyId).find('.erp-line-row').length === 0) {
                $(config.emptyContainerId).show();
            } else {
                $(config.emptyContainerId).hide();
            }
        }

        function renderFefoChips(allocations) {
            if (!allocations || allocations.length === 0) {
                return '<span class="text-danger fw-bold"><i class="bi bi-exclamation-triangle me-1"></i>غير متاح</span>';
            }

            return allocations.map(a => {
                const remaining = a.remaining || a.Remaining || 0;
                const isLow = remaining < 5;
                return `
                    <span class="pos-fefo-tag ${isLow ? 'low-stock' : ''}" title="الباتش: ${a.batchNumber || a.BatchNumber}">
                        باقي: <strong>${remaining}</strong>
                    </span>
                `;
            }).join('');
        }

        return {
            init: init,
            addRow: addRow,
            recalculateAll: recalculateAll,
            getLineCount: function() { return $(config.tableBodyId).find('.erp-line-row').length; },
            getActiveRow: getActiveRow,
            refreshValidation: refreshValidation,
            renderFefoChips: renderFefoChips,
            formatNumber: formatNumber
        };
    })();

    window.PharmacyTransaction = PharmacyTransaction;
})(window, jQuery);
