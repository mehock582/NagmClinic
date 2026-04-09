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

            // Initialize audio context on first user interaction 
            // to ensure it works in modern browsers
            initAudio();

            // Rule 1: Check existing rows
            var existingRow = config.findRowByBarcode ? config.findRowByBarcode(barcode) : null;
            if (existingRow && existingRow.length) {
                if (config.incrementQuantity) config.incrementQuantity(existingRow);
                this.beepIncrement();
                if (config.onSuccess) config.onSuccess('increment', existingRow, barcode, null);
                return;
            }

            // Not found in current UI -> lookup from server
            if (config.onLookupStart) config.onLookupStart(barcode);

            $.get(config.lookupUrl, { barcode: barcode }, function (res) {
                if (res.success && res.data) {
                    // Success single match
                    var targetRow = config.findActiveOrEmptyRow ? config.findActiveOrEmptyRow() : null;
                    if (targetRow && targetRow.length) {
                        if (config.fillRow) config.fillRow(targetRow, res.data, barcode);
                    } else {
                        if (config.addRowWithData) config.addRowWithData(res.data, barcode);
                    }
                    BarcodeScanner.beepSuccess();
                    if (config.onSuccess) config.onSuccess('add_known', targetRow, barcode, res.data);

                } else if (res.multiMatch) {
                    // Multiple batches match
                    if (config.onMultiMatch) {
                        config.onMultiMatch(res.matches, barcode);
                    } else {
                        BarcodeScanner.beepError();
                        if (config.onError) config.onError('multi_match', barcode);
                    }
                } else {
                    // Not found in DB / server
                    var targetRow = config.findActiveOrEmptyRow ? config.findActiveOrEmptyRow() : null;
                    if (targetRow && targetRow.length) {
                        if (config.fillRowBarcodeOnly) config.fillRowBarcodeOnly(targetRow, barcode);
                    } else {
                        if (config.addRowWithBarcodeOnly) config.addRowWithBarcodeOnly(barcode);
                    }
                    BarcodeScanner.beepError();
                    if (config.onError) config.onError('not_found', barcode);
                }
            }).fail(function () {
                BarcodeScanner.beepError();
                if (config.onError) config.onError('network_error', barcode);
            });
        }
    };
})();
