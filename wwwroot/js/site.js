// Sidebar Toggle
var SIDEBAR_MINI_KEY = 'nagmclinic.sidebarMini';

function applySidebarMiniPreference() {
    if (window.innerWidth >= 992 && localStorage.getItem(SIDEBAR_MINI_KEY) === '1') {
        document.body.classList.add('sidebar-mini');
        return;
    }

    document.body.classList.remove('sidebar-mini');
}

function toggleSidebarMini() {
    if (window.innerWidth < 992) return;

    var isMini = document.body.classList.toggle('sidebar-mini');
    localStorage.setItem(SIDEBAR_MINI_KEY, isMini ? '1' : '0');
}

function syncSidebarGroupState() {
    document.querySelectorAll('.sidebar-group').forEach(function (group) {
        var body = group.querySelector('.sidebar-group-body');
        var toggle = group.querySelector('.sidebar-group-toggle');
        var isOpen = !!body && body.classList.contains('show');

        group.classList.toggle('is-open', isOpen);
        if (toggle) {
            toggle.setAttribute('aria-expanded', isOpen ? 'true' : 'false');
        }
    });
}

document.addEventListener('DOMContentLoaded', function () {
    applySidebarMiniPreference();
    syncSidebarGroupState();

    document.querySelectorAll('.sidebar-group-body').forEach(function (groupBody) {
        groupBody.addEventListener('shown.bs.collapse', syncSidebarGroupState);
        groupBody.addEventListener('hidden.bs.collapse', syncSidebarGroupState);
    });

    window.addEventListener('resize', function () {
        applySidebarMiniPreference();
    });

    // Global Double-Submit Prevention
    $(document).on('submit', 'form', function (e) {
        var $form = $(this);
        var $submitBtn = $form.find('button[type="submit"]');

        // Only run jQuery validation if the library is loaded on this specific page
        if (typeof $form.valid === 'function') {
            if (!$form.valid()) {
                return; // Do not disable the button if the form is invalid
            }
        }

        if ($submitBtn.length > 0) {
            $submitBtn.prop('disabled', true);
            $submitBtn.html('<span class="spinner-border spinner-border-sm me-2" role="status" aria-hidden="true"></span> جاري المعالجة...');
        }
    });
});

// Global SweetAlert Delete/Action Confirmation wrapper
function confirmAction(title, text, confirmText, callback) {
    Swal.fire({
        title: title,
        text: text,
        icon: 'question',
        showCancelButton: true,
        confirmButtonColor: '#4361ee',
        cancelButtonColor: '#d33',
        confirmButtonText: confirmText,
        cancelButtonText: 'إلغاء'
    }).then((result) => {
        if (result.isConfirmed) {
            callback();
        }
    });
}

// ────────────────────────────────────────────────
// Phase 3: Global Quick-Create Patient (DRY)
// ────────────────────────────────────────────────
// Each page using the shared partial can set:
//   window.onPatientCreated = function(id, name, phone) { ... }
// to receive the newly created patient.
function quickCreatePatient() {
    var form = document.getElementById('quickPatientForm');
    if (form && !form.checkValidity()) {
        form.classList.add('was-validated');
        return;
    }

    var data = {
        fullName: $('#quickPatientName').val(),
        phoneNumber: $('#quickPatientPhone').val(),
        genderString: $('#quickPatientGender').val(),
        address: $('#quickPatientAddress').val(),
        age: $('#quickPatientAge').val()
    };

    var $btn = $('#newPatientModal .btn-primary');
    $btn.prop('disabled', true).html('<i class="bi bi-arrow-repeat spin me-1"></i> جاري الحفظ...');

    $.post('/Patients/QuickCreate', data, function (response) {
        $btn.prop('disabled', false).html('<i class="bi bi-check-lg me-1"></i> حفظ المريض');
        if (response.success) {
            // Close modal & reset
            var modalEl = document.getElementById('newPatientModal');
            var modal = bootstrap.Modal.getInstance(modalEl);
            if (modal) modal.hide();

            $('#quickPatientName, #quickPatientPhone, #quickPatientAddress, #quickPatientAge').val('');
            $('#quickPatientModalError').hide();
            if (form) form.classList.remove('was-validated');

            // Notify caller
            if (typeof window.onPatientCreated === 'function') {
                window.onPatientCreated(response.patientId, response.fullName, data.phoneNumber);
            } else {
                // Default: reload
                location.reload();
            }

            showToast('success', 'تم حفظ المريض بنجاح');
        } else {
            $('#quickPatientModalError').text(response.message).show();
        }
    }).fail(function () {
        $btn.prop('disabled', false).html('<i class="bi bi-check-lg me-1"></i> حفظ المريض');
        $('#quickPatientModalError').text('حدث خطأ في النظام. يرجى مراجعة البيانات.').show();
    });
}

// ────────────────────────────────────────────────
// Phase 4: Global SweetAlert Toast Notifications
// ────────────────────────────────────────────────
function showToast(icon, message, timer) {
    Swal.fire({
        toast: true,
        position: 'top-end',
        icon: icon,
        title: message,
        showConfirmButton: false,
        timer: timer || 2500,
        timerProgressBar: true,
        didOpen: function (toast) {
            toast.addEventListener('mouseenter', Swal.stopTimer);
            toast.addEventListener('mouseleave', Swal.resumeTimer);
        }
    });
}

// ────────────────────────────────────────────────
// Phase 5: Global DataTables Action Buttons
// ────────────────────────────────────────────────
function renderActionButtons(id, baseUrl) {
    var token = $('input[name="__RequestVerificationToken"]').val() || '';
    return `
        <div class="action-group">
            <a href="${baseUrl}/Details/${id}" class="btn-action btn-action-info" data-label="التفاصيل">
                <i class="bi bi-file-earmark-text"></i>
            </a>
            <a href="${baseUrl}/Edit/${id}" class="btn-action btn-action-warning" data-label="تعديل">
                <i class="bi bi-pencil-fill"></i>
            </a>
            <form action="${baseUrl}/Delete/${id}" method="post" style="display:inline;" id="deleteForm_${id}">
                <input type="hidden" name="__RequestVerificationToken" value="${token}">
                <button type="button" class="btn-action btn-action-danger" data-label="حذف" 
                        onclick="confirmAction('تأكيد الحذف', 'هل أنت متأكد من حذف هذا السجل؟', 'نعم، احذف', function() { document.getElementById('deleteForm_${id}').submit(); })">
                    <i class="bi bi-trash"></i>
                </button>
            </form>
            <a href="${baseUrl}/Details/${id}?print=true" class="btn-action btn-action-dark" data-label="طباعة">
                <i class="bi bi-printer"></i>
            </a>
        </div>
    `;
}

function renderPharmacyActionButtons(id, baseUrl, type) {
    if (type === 'sales') {
        return `
            <div class="action-group">
                <a href="${baseUrl}/Details/${id}" class="btn-action btn-action-info" data-label="تفاصيل الفاتورة">
                    <i class="bi bi-receipt-cutoff"></i>
                </a>
                <a href="${baseUrl}/Edit/${id}" class="btn-action btn-action-warning" data-label="تعديل">
                    <i class="bi bi-pencil-fill"></i>
                </a>
                <a href="${baseUrl}/PrintThermalReceipt/${id}" target="_blank" class="btn-action btn-action-success" data-label="طباعة إيصال">
                    <i class="bi bi-printer-fill"></i>
                </a>
            </div>
        `;
    } else if (type === 'purchases') {
        return `
            <div class="action-group">
                <a href="${baseUrl}/Details/${id}" class="btn-action btn-action-info" data-label="تفاصيل الفاتورة">
                    <i class="bi bi-receipt-cutoff"></i>
                </a>
                <a href="${baseUrl}/Edit/${id}" class="btn-action btn-action-warning" data-label="تعديل">
                    <i class="bi bi-pencil-fill"></i>
                </a>
            </div>
        `;
    }
    return '';
}

function renderModalEditButton(id, onclickFunction, dataAttributes = '') {
    return `<div class="action-group"><button type="button" class="btn-action btn-action-primary" data-label="تعديل" data-id="${id}" ${dataAttributes} onclick="${onclickFunction}(this)"><i class="bi bi-pencil-fill"></i></button></div>`;
}
// ────────────────────────────────────────────────
// Phase 6: Global AJAX Modal Framework
// ────────────────────────────────────────────────
$(document).on('click', '[data-ajax-modal="true"]', function (e) {
    e.preventDefault();
    var $btn = $(this);
    var url = $btn.attr('href') || $btn.data('url');
    var title = $btn.attr('title') || $btn.data('title') || 'تعديل البيانات';

    $('#appModalLabel').text(title);
    // Remove any previously relocated footer
    $('#appModal .modal-content > .modal-footer').remove();
    $('#appModalBody').html('<div class="text-center py-5"><div class="spinner-border text-primary" role="status"></div><p class="mt-2 text-muted">جاري تحميل البيانات...</p></div>');
    $('#appModal').modal('show');

    $.get(url, function (data) {
        $('#appModalBody').html(data);

        // Relocate .modal-footer to be a direct child of .modal-content
        // so that modal-dialog-scrollable can pin it at the bottom
        var $footer = $('#appModalBody').find('.modal-footer').detach();
        if ($footer.length) {
            $('#appModal .modal-content').append($footer);
            // Re-associate submit buttons with their form via the form attribute
            var formId = $('#appModalBody').find('form').attr('id');
            if (formId) {
                $footer.find('button[type="submit"]').attr('form', formId);
            }
        }

        // Finalize form in modal
        var $form = $('#appModalBody').find('form');
        
        // Reparse validation for the new form
        if (typeof $.validator !== 'undefined' && typeof $.validator.unobtrusive !== 'undefined') {
            $.validator.unobtrusive.parse($form);
        }

        // Handle AJAX form submission
        $form.on('submit', function (formEvt) {
            if ($form.data('ajax-mode') === 'traditional') return true; // allow normal submit if requested

            formEvt.preventDefault();
            
            // Defensively check for jQuery validation before validating
            if (typeof $form.valid === 'function') {
                if (!$form.valid()) return false;
            }

            // Search for submit button in the whole modal (it may be in the relocated footer)
            var $submitBtn = $('#appModal').find('button[type="submit"]');
            var originalBtnHtml = $submitBtn.html();
            $submitBtn.prop('disabled', true).html('<span class="spinner-border spinner-border-sm me-2"></span> جاري الحفظ...');

            $.ajax({
                url: $form.attr('action'),
                type: $form.attr('method') || 'POST',
                data: $form.serialize(),
                success: function (response) {
                    if (response.success) {
                        $('#appModal').modal('hide');
                        showToast('success', response.message || 'تمت العملية بنجاح');
                        
                        // Refresh DataTables if present
                        if (typeof $.fn.DataTable !== 'undefined') {
                            $('.dataTable').DataTable().ajax.reload(null, false);
                            // Also check if there's a specific table ID we should reload
                            if (window.onModalSuccess) window.onModalSuccess(response);
                            else if ($('#usersTable').length) location.reload(); // fallback for static tables
                        } else {
                            location.reload();
                        }
                    } else {
                        // If validation fails on server or custom error
                        if (response.errors) {
                            // Handle errors if returned as JSON
                            Swal.fire('خطأ', response.errors.join('<br>'), 'error');
                        } else {
                            // If response is a PartialView (ModelState error)
                            $('#appModalBody').html(response);
                            $.validator.unobtrusive.parse($('#appModalBody').find('form'));
                        }
                    }
                },
                error: function () {
                    Swal.fire('خطأ', 'حدث خطأ غير متوقع في النظام', 'error');
                },
                complete: function () {
                    $submitBtn.prop('disabled', false).html(originalBtnHtml);
                }
            });
            return false;
        });
    }).fail(function () {
        $('#appModalBody').html('<div class="alert alert-danger">فشل تحميل البيانات. يرجى المحاولة مرة أخرى.</div>');
    });
});

// Clean up relocated footer when modal closes
$('#appModal').on('hidden.bs.modal', function () {
    $(this).find('.modal-content > .modal-footer').remove();
});
