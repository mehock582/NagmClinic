// Sidebar Toggle
var SIDEBAR_MINI_KEY = 'nagmclinic.sidebarMini';

function toggleSidebar() {
    var sidebar = document.getElementById('sidebar');
    var overlay = document.getElementById('sidebarOverlay');
    if (!sidebar || !overlay) return;

    sidebar.classList.toggle('open');
    overlay.classList.toggle('show');
}

function closeSidebarOnMobile() {
    if (window.innerWidth >= 992) return;

    var sidebar = document.getElementById('sidebar');
    var overlay = document.getElementById('sidebarOverlay');
    if (!sidebar || !overlay) return;

    sidebar.classList.remove('open');
    overlay.classList.remove('show');
}

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

    document.querySelectorAll('#sidebar .nav-item').forEach(function (link) {
        link.addEventListener('click', closeSidebarOnMobile);
    });

    window.addEventListener('resize', function () {
        applySidebarMiniPreference();
        closeSidebarOnMobile();
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

// Auto-fire TempData toasts on page load
$(function () {
    var successMsg = $('meta[name="toast-success"]').attr('content');
    var errorMsg = $('meta[name="toast-error"]').attr('content');
    if (successMsg) showToast('success', successMsg);
    if (errorMsg) showToast('error', errorMsg);
});
