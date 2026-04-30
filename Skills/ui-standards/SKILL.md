# SKILL: UI/UX Standards — RTL Arabic Bootstrap 5

## Trigger
Activate when creating/modifying Views (.cshtml), CSS, JavaScript, layouts, or any front-end code.

---

## 1. Language & Direction

- **All UI text MUST be in Arabic.** No English labels, buttons, or messages in the user-facing interface.
- **RTL layout** is enforced via `bootstrap-rtl.min.css` (loaded in `_Layout.cshtml`).
- **Font:** Cairo (loaded via `cairo-font.css`). Never use system defaults.

---

## 2. Layout Structure

The master layout is `Views/Shared/_Layout.cshtml`. It provides:
- Sidebar navigation (`_Sidebar.cshtml`) — collapsible, role-aware.
- Login partial (`_LoginPartial.cshtml`).
- Breadcrumbs (`_Breadcrumbs.cshtml`).
- Alert toast system (reads `TempData["AlertMessage"]` + `TempData["AlertType"]`).

### Alert System (from `BaseController`)

```csharp
// In controller:
ShowAlert("تمت العملية بنجاح");           // success (default)
ShowAlert("حدث خطأ ما", "error");         // error
ShowAlert("تنبيه مهم", "warning");         // warning
```

These display as Bootstrap toasts on the next page load. The layout reads `TempData["AlertMessage"]` and `TempData["AlertType"]`.

---

## 3. Form Standards

### Card Wrapper

All forms MUST be wrapped in a Bootstrap card:

```html
<div class="card shadow-sm">
    <div class="card-header">
        <h5 class="mb-0"><i class="bi bi-icon-name me-2"></i>عنوان النموذج</h5>
    </div>
    <div class="card-body">
        <form asp-action="Create" method="post">
            @Html.AntiForgeryToken()
            <!-- form fields -->
        </form>
    </div>
</div>
```

### Input Classes

```html
<div class="mb-3">
    <label asp-for="FieldName" class="form-label"></label>
    <input asp-for="FieldName" class="form-control" />
    <span asp-validation-for="FieldName" class="text-danger"></span>
</div>
```

### Select Dropdowns

```html
<select asp-for="CategoryId" class="form-select" asp-items="ViewBag.Categories">
    <option value="">-- اختر --</option>
</select>
```

### Submit Buttons

```html
<button type="submit" class="btn btn-primary">
    <i class="bi bi-check-circle me-1"></i>حفظ
</button>
<a asp-action="Index" class="btn btn-secondary">
    <i class="bi bi-x-circle me-1"></i>إلغاء
</a>
```

---

## 4. Validation

- **Server-side:** Use DataAnnotation attributes with Arabic `ErrorMessage`:
  ```csharp
  [Required(ErrorMessage = "الحقل مطلوب")]
  [MaxLength(100, ErrorMessage = "الحد الأقصى 100 حرف")]
  ```
- **Client-side:** jQuery Validation + Unobtrusive validation loaded via `_ValidationScriptsPartial.cshtml`.
- All Identity errors are Arabic (via `ArabicIdentityErrorDescriber`).

---

## 5. DataTables Grid Pattern

All data grids use DataTables.js with server-side processing.

### Standard Table HTML

```html
<table id="myDataTable" class="table table-hover align-middle" style="width:100%">
    <thead>
        <tr>
            <th>العمود الأول</th>
            <th>العمود الثاني</th>
            <th>إجراءات</th>
        </tr>
    </thead>
</table>
```

### Standard JS Initialization

```javascript
$('#myDataTable').DataTable({
    processing: true,
    serverSide: true,
    ajax: {
        url: '/Controller/GetData',
        type: 'POST',
        headers: { 'RequestVerificationToken': $('input[name="__RequestVerificationToken"]').val() }
    },
    columns: [
        { data: 'column1' },
        { data: 'column2' },
        {
            data: 'id',
            orderable: false,
            render: function(data) {
                return `<a href="/Controller/Details/${data}" class="btn btn-sm btn-outline-primary">
                    <i class="bi bi-eye"></i>
                </a>`;
            }
        }
    ],
    language: { url: '//cdn.datatables.net/plug-ins/1.13.7/i18n/ar.json' },
    order: [[0, 'desc']]
});
```

The shared DataTables config is in `wwwroot/js/shared-datatable.js`.

---

## 6. Double-Click Prevention

The global `wwwroot/js/site.js` disables form submit buttons after first click to prevent duplicates. **Do NOT add your own submit-disabling logic.**

If a button needs to avoid this behavior (e.g., a non-submit button inside a form), ensure it has `type="button"` (not `type="submit"`).

Conditional check exists so buttons outside jQuery Validation contexts don't crash.

---

## 7. Modal Dialogs Pattern

Used for User CRUD and quick-create actions (e.g., Quick Create Patient):

```html
<!-- Trigger -->
<button type="button" class="btn btn-primary" data-bs-toggle="modal" data-bs-target="#myModal">
    فتح
</button>

<!-- Modal -->
<div class="modal fade" id="myModal" tabindex="-1">
    <div class="modal-dialog">
        <div class="modal-content" id="modalContent">
            <!-- Loaded via AJAX -->
        </div>
    </div>
</div>
```

Content is loaded via AJAX into `#modalContent` from a PartialView returned by the controller.

---

## 8. Shared Partials

| Partial | Purpose |
|---------|---------|
| `_PharmacyLineItem.cshtml` | Reusable line-item row for pharmacy sale/purchase forms |
| `_QuickCreatePatientModal.cshtml` | Modal for creating a patient inline during appointment booking |
| `_PrintBrandingHeader.cshtml` | Clinic header for printed receipts/reports |
| `_PrintBrandingFooter.cshtml` | Clinic footer for printed receipts/reports |
| `_Breadcrumbs.cshtml` | Dynamic breadcrumb trail |
| `_Sidebar.cshtml` | Role-aware navigation sidebar |

When adding new reusable UI blocks, create them as Shared partials.

---

## 9. Print Styles

Separate CSS files exist for print contexts:
- `lab-report-print.css` — Lab result printouts
- `thermal-receipt.css` — Thermal receipt (80mm) format for pharmacy sales
- `print-branding.css` — Clinic branding (logo, header/footer) in printed output

Use `@media print` rules and load print-specific CSS only in the relevant views.

---

## 10. JavaScript Files

| File | Purpose |
|------|---------|
| `site.js` | Global utilities: sidebar toggle, alert toasts, form submit protection, general helpers |
| `pharmacy-transaction.js` | Pharmacy sale/purchase form logic: line management, barcode scanning, totals calculation |
| `barcode-scanner.js` | Hardware barcode scanner integration |
| `erp-barcode-scanner.js` | ERP-specific barcode scanning workflow |
| `shared-datatable.js` | Shared DataTables configuration defaults |

### Corresponding CSS

| File | Purpose |
|------|---------|
| `site.css` | Main application styles |
| `pharmacy-transaction.css` | Pharmacy form-specific styles |
| `cairo-font.css` | Cairo Arabic font loading |
| `bootstrap-rtl.min.css` | RTL Bootstrap 5 |

---

## 11. Icons

Use **Bootstrap Icons** (`bi bi-*`). They're loaded globally. Examples:
- `bi bi-plus-circle` — Add/Create
- `bi bi-pencil-square` — Edit
- `bi bi-trash` — Delete
- `bi bi-eye` — View/Details
- `bi bi-printer` — Print
- `bi bi-search` — Search
- `bi bi-check-circle` — Save/Confirm

---

## 12. ViewData Conventions

```csharp
ViewData["Title"] = "عنوان الصفحة";  // Always set in controller action
```

The layout renders this in `<title>` and breadcrumbs. Every action must set it.
