/**
 * Shared Barcode Scanning Workflow for ERP/Pharmacy.
 * Provides unified audio feedback and generic workflow handler rules.
 */
var BarcodeScanner = (function () {
    var audioCtx = null;

    function initAudio() {
        if (!audioCtx) {
            try {
                audioCtx = new (window.AudioContext || window.webkitAudioContext)();
            } catch (e) {
                // Audio not supported
            }
        }
        if (audioCtx && audioCtx.state === 'suspended') {
            audioCtx.resume();
        }
    }

    function playBeep(freq, duration, type) {
        try {
            initAudio();
            if (!audioCtx) return;
            
            var osc = audioCtx.createOscillator();
            var gain = audioCtx.createGain();
            osc.connect(gain);
            gain.connect(audioCtx.destination);
            osc.type = type || 'sine';
            osc.frequency.value = freq || 800;
            gain.gain.value = 0.12;
            osc.start();
            osc.stop(audioCtx.currentTime + (duration || 0.12));
        } catch (e) { /* ignore */ }
    }

    return {
        beepSuccess: function () {
            playBeep(880, 0.1, 'sine');
            setTimeout(() => playBeep(1100, 0.12, 'sine'), 110);
        },
        beepError: function () {
            playBeep(300, 0.25, 'square');
        },
        beepIncrement: function () {
            playBeep(660, 0.08, 'sine');
        },

        /**
         * Generic scan handler implementing rules:
         * 1) If same barcode exists in current form -> increase qty.
         * 2) If new and empty row exists -> fill row.
         * 3) If new and no empty row -> create new row/fill.
         */
        processScan: function (barcode, config) {
            barcode = (barcode || '').trim();
            if (!barcode) return;

            initAudio();
            console.log('[BarcodeScanner] Scanning:', barcode);

            // Step 1: Find if row already exists for this barcode
            var existingRow = config.findRowByBarcode ? config.findRowByBarcode(barcode) : null;
            if (existingRow && existingRow.length) {
                console.log('[BarcodeScanner] Found existing row, incrementing');
                if (config.incrementQuantity) config.incrementQuantity(existingRow);
                this.beepIncrement();
                if (config.onSuccess) config.onSuccess('increment', existingRow, barcode, null);
                return;
            }

            // Step 2: NEW Barcode - Claim a row immediately
            var targetRow = config.findActiveOrEmptyRow ? config.findActiveOrEmptyRow() : null;
            if (!targetRow || targetRow.length === 0) {
                console.log('[BarcodeScanner] No empty row, creating new one');
                if (config.createNewRow) targetRow = config.createNewRow();
            }

            if (!targetRow || targetRow.length === 0) {
                console.error('[BarcodeScanner] Failed to find or create a row.');
                return;
            }

            // Step 3: CLAIM the row immediately with the barcode string
            // This prevents the next scan (if very fast) from ignoring this row or overwriting it.
            var targetRowId = targetRow.attr('id');
            targetRow.attr('data-barcode', barcode);
            targetRow.find('[data-field="Barcode"]').val(barcode);
            console.log('[BarcodeScanner] Assigned barcode to row:', targetRowId);

            if (config.onLookupStart) config.onLookupStart(barcode);

            // Step 4: Server lookup for item details
            $.get(config.lookupUrl, { barcode: barcode }, function (res) {
                var success = res.success || res.Success;
                var data = res.data || res.Data;
                var multiMatch = res.multiMatch || res.MultiMatch;

                // Re-select by ID to ensure we have the live element
                var $row = $('#' + targetRowId);

                if (success && data) {
                    console.log('[BarcodeScanner] Lookup success, filling row');
                    if (config.fillRow) config.fillRow($row, data, barcode);
                    BarcodeScanner.beepSuccess();
                    if (config.onSuccess) config.onSuccess('add_known', $row, barcode, data);
                } else if (multiMatch) {
                    console.log('[BarcodeScanner] Multi-match found');
                    if (config.onMultiMatch) config.onMultiMatch(res.matches || res.Matches, barcode);
                } else {
                    console.warn('[BarcodeScanner] Barcode not found on server');
                    BarcodeScanner.beepError();
                    if (config.onError) config.onError('not_found', barcode, $row);
                }
            }).fail(function () {
                BarcodeScanner.beepError();
                if (config.onError) config.onError('network_error', barcode, $('#' + targetRowId));
            });
        }
    };
})();
