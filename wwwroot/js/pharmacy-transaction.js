/**
 * Shared JS for Pharmacy Transactions (Purchases & Sales)
 * Handles row addition, removal, reindexing, and calculating.
 */
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
            var $row = $(this).closest('.erp-line-row');
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
        var idx = config.lineCount;

        // Replace placeholders if they are generic, normally we inject idx later.
        var rowHtml = templateHtml.replace(/\{idx\}/g, idx);

        var $newRow = $(rowHtml);
        $newRow.attr('id', 'line_row_' + idx);
        $newRow.attr('data-line-index', idx);
        
        // Ensure barcode tracking for shared scanner integration
        if (prefillData && prefillData.barcode) {
            $newRow.attr('data-barcode', prefillData.barcode);
            $newRow.find('[data-field="Barcode"]').val(prefillData.barcode);
        }

        $(config.tableBodyId).append($newRow);
        config.lineCount++;

        toggleEmptyState();

        if (config.callbacks.onRowAdded) {
            config.callbacks.onRowAdded($newRow, prefillData, idx);
        }

        refreshValidation();
        return $newRow;
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

        var displayVal = total.toFixed(2);
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
                    // Update ID if it exists and follows the convention
                    if ($field.attr('id')) {
                        $field.attr('id', 'Lines_' + index + '__' + fieldName);
                    }
                }
            });

            // Update validation fields data bindings
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
            $('#linesClientValidation').text('');
        } else {
            $(config.emptyContainerId).hide();
            $('#linesClientValidation').text('');
        }
    }

    return {
        init: init,
        addRow: addRow,
        recalculateAll: recalculateAll,
        getLineCount: function() { return config.lineCount; },
        refreshValidation: refreshValidation
    };
})();
