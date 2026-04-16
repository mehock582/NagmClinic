(function(window, $) {
    let barcodeBuffer = "";
    let scanTimeout = null;
    let isScannerLocked = false;
    let targetInputAtStart = null;
    let originalInputValue = "";

    window.initializeGlobalScanner = function(onBarcodeScannedCallback) {
        // .off() ensures we don't accidentally bind multiple times
        $(document).off('keydown.erpScanner').on('keydown.erpScanner', function(e) {
            
            if (isScannerLocked) return;

            // 1. Capture the state of the UI before the scanner ruins it
            if (barcodeBuffer === "") {
                let activeEl = document.activeElement;
                if (activeEl && (activeEl.tagName === 'INPUT' || activeEl.tagName === 'TEXTAREA')) {
                    targetInputAtStart = activeEl;
                    originalInputValue = $(activeEl).val();
                } else {
                    targetInputAtStart = null;
                    originalInputValue = "";
                }
            }

            // 2. Aggregate printable characters
            if (e.key.length === 1) {
                barcodeBuffer += e.key;

                clearTimeout(scanTimeout);
                // INCREASED TIMEOUT TO 250ms: Crucial for Bluetooth/Wireless scanners
                scanTimeout = setTimeout(function() {
                    barcodeBuffer = "";
                    targetInputAtStart = null;
                    originalInputValue = "";
                }, 250); 
            }

            // 3. The scanner hit Enter
            if (e.keyCode === 13 || e.key === 'Enter') {
                if (barcodeBuffer.length >= 5) {
                    // STOP EVERYTHING. Prevent forms, prevent other scripts.
                    e.preventDefault();
                    e.stopPropagation(); 

                    isScannerLocked = true;
                    let finalBarcode = barcodeBuffer;

                    // Restore the corrupted input (unless they were typing in the main search box)
                    if (targetInputAtStart && !$(targetInputAtStart).hasClass('main-barcode-search')) {
                        $(targetInputAtStart).val(originalInputValue);
                    }

                    // Wipe the buffer clean immediately
                    barcodeBuffer = "";
                    clearTimeout(scanTimeout);

                    // Execute the lookup, pass the unlock function
                    onBarcodeScannedCallback(finalBarcode, function unlock() {
                        isScannerLocked = false;
                    });
                }
                
                // If Enter was hit but buffer was empty/short, we don't block it 
                // but we should clear buffer for consistency
                if (barcodeBuffer.length > 0 && barcodeBuffer.length < 5) {
                    barcodeBuffer = "";
                }
            }
        });
    };
})(window, jQuery);
