# SKILL: Responsive Mobile Design — RTL Arabic ERP

Verification token: MOBILE-SKILL-0429

## Trigger
Activate when creating/modifying ANY View (.cshtml), CSS, or JavaScript that renders UI. EVERY page must be mobile-ready. This skill defines the mandatory patterns to ensure the browser renders the application correctly across phones, tablets, and desktops.

When touching paired pharmacy POS views, treat `PharmacyPurchases` and `PharmacySales` as a mirrored UX surface. If one receives layout, structure, JavaScript, sticky footer, or Bootstrap grid changes, inspect the sibling view and apply the same UX-level changes unless the user explicitly asks to keep them different.

---

## 1. Foundation: What's Already Responsive (Don't Rebuild)

The layout stack is already mobile-aware. Do NOT rewrite these — just use them correctly:

| Component | Mobile Behavior | Implementation |
|-----------|----------------|----------------|
| **Sidebar** | Bootstrap 5 Offcanvas (`offcanvas-md offcanvas-end`) | `_Sidebar.cshtml` — auto-hides on `<md`, triggered by hamburger button |
| **Main Wrapper** | Full-width on mobile (`margin-right: 0`) | `site.css` line 644–654: `@media (max-width: 767.98px)` |
| **Modals** | Auto-fullscreen on mobile | `site.css` line 604–627: `@media (max-width: 767.98px)` + `modal-fullscreen-md-down` class |
| **DataTables** | Responsive extension auto-collapses columns | `responsive: true` in `shared-datatable.js`, expand icon styled in `site.css` |
| **Input Groups** | `flex-wrap: nowrap` on mobile | `site.css` line 476–485 |
| **Topbar** | Breadcrumbs hidden on `<md` (`d-none d-md-block`), hamburger shown (`d-md-none`) | `_Layout.cshtml` lines 42–55 |

---

## 2. Breakpoint System (Bootstrap 5 RTL)

Use ONLY these standard Bootstrap 5 breakpoints:

| Token | Min-Width | CSS Class Infix | Use For |
|-------|-----------|----------------|---------|
| `xs` | 0 | *(none)* | Phones portrait |
| `sm` | 576px | `-sm-` | Phones landscape |
| `md` | 768px | `-md-` | Tablets |
| `lg` | 992px | `-lg-` | Desktops |
| `xl` | 1200px | `-xl-` | Wide desktops |

### Media Query Convention (mobile-first)
```css
/* Phone-only override */
@media (max-width: 575.98px) { ... }

/* Tablet and below */
@media (max-width: 767.98px) { ... }

/* Desktop collapse point */
@media (max-width: 991.98px) { ... }
```

**CRITICAL:** Always use `max-width` with `.98px` fractional values (Bootstrap convention) to avoid 1px overlap.

---

## 3. Grid Layout Rules

### 3.1 Use Bootstrap `row` + `col-*` for Form Layouts

```html
<div class="row g-2">
    <div class="col-md-6"><!-- Full width on mobile, half on tablet+ --></div>
    <div class="col-md-3"><!-- Full width on mobile, quarter on tablet+ --></div>
    <div class="col-md-3"><!-- Full width on mobile, quarter on tablet+ --></div>
</div>
```

**Rules:**
- ALWAYS start with mobile (`col-12` is implicit).
- Add `col-md-*` or `col-lg-*` for larger breakpoints.
- Use `g-2` or `g-3` for gutters (consistent spacing).
- NEVER use fixed pixel widths on form fields.

### 3.2 Use CSS Grid for Complex Card Layouts (with mobile override)

Existing pattern from appointment edit (`site.css`):
```css
/* Desktop: 5-column grid */
.appt-item-row {
    display: grid;
    grid-template-columns: 1fr 80px 100px 90px 40px;
    gap: 0.5rem;
}

/* Tablet: tighter columns */
@media (max-width: 991.98px) {
    .appt-item-row {
        grid-template-columns: 1fr 60px 80px 70px 36px;
    }
}

/* Phone: stack to 2 columns */
@media (max-width: 575.98px) {
    .appt-item-row {
        grid-template-columns: 1fr 1fr;
    }
    .appt-item-select {
        grid-column: 1 / -1; /* full width */
    }
}
```

### 3.3 Pharmacy POS Line Cards

The pharmacy line card collapses from 6-column grid to single-column on tablet:
```css
/* Desktop */
.pos-line-card {
    grid-template-columns: 2.5fr 1fr 1.2fr 2fr 1.5fr 40px;
}

/* Tablet and below: stack */
@media (max-width: 991.98px) {
    .pos-line-card {
        grid-template-columns: 1fr;
    }
}
```

**When creating new grid-based cards, ALWAYS include a mobile override.**

---

## 4. Sidebar-Aware Content Margins

The sidebar is `260px` wide (or `86px` collapsed). Content has `margin-right: var(--sidebar-width)`.

**On mobile (`<768px`):** The sidebar becomes an offcanvas overlay, so:
```css
@media (max-width: 767.98px) {
    .main-wrapper {
        margin-right: 0 !important;
        width: 100%;
    }
}
```

This is already handled globally. **Never add sidebar-compensating margins in individual views.**

---

## 5. UX Symmetry & Module Mirroring

The `PharmacyPurchases` and `PharmacySales` POS modules share one UX language. Their layout, hierarchy, footer behavior, scanner flow, and responsive behavior must stay visually and structurally aligned unless there is a documented business reason for divergence.

### Purchase/Sale Mirror Rule

- Any structural, layout, JavaScript, or Bootstrap grid change made to `PharmacyPurchases/Create.cshtml` MUST be reviewed for `PharmacySales/Create.cshtml`.
- Any structural, layout, JavaScript, or Bootstrap grid change made to `PharmacySales/Create.cshtml` MUST be reviewed for `PharmacyPurchases/Create.cshtml`.
- If a change affects scanner placement, card hierarchy, footer actions, line-item structure, spacing, or breakpoint behavior, mirror it in the paired module.
- Only allow intentional differences when the user explicitly asks for divergence or the underlying business workflow truly requires different controls.

### Actionable Directive

When asked to modify the layout of one pharmacy POS screen, proactively inspect the sibling screen and either:

1. Apply the same UX/layout changes there as part of the same task.
2. If mirroring would be risky because the sibling screen has meaningful workflow differences, pause and call out the mismatch before editing.

---

## 6. Sticky Footer Pattern (Mobile Summary Bars)

For pages with financial totals (appointments, pharmacy), use a **sticky footer bar** that shows on mobile when the desktop sidebar summary is hidden:

```css
/* Hidden by default (desktop shows sidebar summary) */
.appt-sticky-footer {
    display: none;
    position: fixed;
    bottom: 0;
    left: 0;
    right: 0;
    z-index: 1030;
    background: rgba(255, 255, 255, 0.97);
    backdrop-filter: blur(12px);
    border-top: 1px solid var(--border-color);
    padding: 0.6rem 1.25rem;
}

/* Show on tablet and below */
@media (max-width: 991.98px) {
    .appt-sticky-footer { display: flex; }
    .appt-edit-sidebar { display: none; }
}
```

**When building new financial pages:**
1. Create a desktop sidebar summary panel.
2. Create a mobile sticky footer with the same totals.
3. Toggle visibility via `@media (max-width: 991.98px)`.
4. Add `padding-bottom: 120px` on the page body to prevent content being hidden behind the sticky footer.

---

## 7. Table Responsiveness

### 7.1 DataTables (Server-Side Grids)

All DataTables MUST include the Responsive extension:
```javascript
initDataTable('#myTable', '/Controller/GetData', columns, {
    responsive: true
});
```

The Responsive extension is already loaded globally in `_Layout.cshtml`. It auto-hides columns that don't fit and shows an expand icon.

**Column priority tips:**
- Use `responsivePriority` in column definitions to control which columns collapse first.
- Action columns should have `responsivePriority: 1` (never hidden).
- Metadata columns (dates, IDs) should have higher numbers (hidden first).

```javascript
columns: [
    { data: 'name', responsivePriority: 1 },
    { data: 'date', responsivePriority: 3 },
    { data: 'actions', orderable: false, responsivePriority: 1 }
]
```

### 7.2 Static Tables

Wrap static tables in `table-responsive`:
```html
<div class="table-responsive">
    <table class="table table-hover align-middle">...</table>
</div>
```

---

## 8. Button Groups on Mobile

### Action Buttons Row
```html
<div class="d-flex justify-content-center gap-3 flex-wrap mb-4">
    <button class="btn btn-primary btn-lg rounded-pill fw-bold px-4">حفظ</button>
    <a class="btn btn-outline-secondary btn-lg rounded-pill fw-bold px-4">إلغاء</a>
</div>
```

**Key:** `flex-wrap` ensures buttons stack on very small screens. `gap-3` maintains spacing.

### DataTable Row Actions
Use the `.action-group` class (already has `flex-wrap: wrap`):
```html
<div class="action-group">
    <a class="btn-action btn-action-primary" data-label="تفاصيل"><i class="bi bi-eye"></i></a>
    <a class="btn-action btn-action-warning" data-label="تعديل"><i class="bi bi-pencil"></i></a>
</div>
```

---

## 9. Text Truncation & Overflow

Arabic text can be long. Prevent layout breaks:

```css
/* Already applied to sidebar nav text */
.nav-text {
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
}
```

**For table cells or card content:**
```css
.text-truncate-cell {
    max-width: 200px;
    white-space: nowrap;
    overflow: hidden;
    text-overflow: ellipsis;
}

@media (max-width: 575.98px) {
    .text-truncate-cell {
        max-width: 120px;
    }
}
```

---

## 10. Touch Target Sizes

Mobile users tap with fingers. Minimum touch targets:

| Element | Minimum Size | How |
|---------|-------------|-----|
| Buttons | 44×44px | Use `btn-lg` or explicit `min-height: 44px` |
| Action icons | 34×34px | `.btn-action` is already `34×34px` |
| Form inputs | 38px height | Bootstrap default `form-control` |
| Checkbox/Radio | 20×20px | Bootstrap default |

**Never make clickable elements smaller than 34px on mobile.**

---

## 11. Image & Media Responsiveness

```css
img { max-width: 100%; height: auto; }
```

For clinic logos in print headers:
```html
<img src="@branding.LogoPath" class="img-fluid" style="max-height: 60px;" alt="شعار المستوصف">
```

---

## 12. Viewport Meta Tag

Already set in `_Layout.cshtml` — **NEVER remove or modify:**
```html
<meta name="viewport" content="width=device-width, initial-scale=1.0" />
```

---

## 13. Hide/Show Utilities (Bootstrap)

Use Bootstrap responsive display utilities instead of custom CSS when possible:

| Class | Meaning |
|-------|---------|
| `d-none d-md-block` | Hidden on mobile, visible on tablet+ |
| `d-md-none` | Visible on mobile only |
| `d-none d-lg-flex` | Hidden below desktop |
| `d-flex d-md-none` | Flex on mobile only |

**Already used in layout:**
- Breadcrumbs: `d-none d-md-block`
- Hamburger button: `d-md-none`
- Username in topbar: `d-none d-sm-inline`

---

## 14. Checklist: Before Submitting Any New View

Run through this checklist for EVERY new `.cshtml` file:

- [ ] **Viewport:** Does the page render correctly at 375px width? (iPhone SE)
- [ ] **Forms:** Are all `row`/`col-*` grids mobile-first? (`col-12` → `col-md-6`)
- [ ] **Tables:** Is DataTables `responsive: true` enabled? Or is `table-responsive` wrapper used?
- [ ] **Cards:** Do cards stack vertically on mobile? (no horizontal overflow)
- [ ] **Buttons:** Are action buttons wrapped with `flex-wrap`?
- [ ] **Text:** Is long text truncated or wrapped? (no horizontal scrollbar)
- [ ] **Modals:** Does the modal use `modal-fullscreen-md-down` or the global fullscreen override?
- [ ] **Sticky Footer:** If financial page, is there a mobile sticky footer for totals?
- [ ] **Mirror Rule:** If this is `PharmacyPurchases` or `PharmacySales`, were matching UX/layout changes applied to the paired module?
- [ ] **Touch Targets:** Are all tappable elements ≥ 34px?
- [ ] **No Fixed Widths:** Are all widths relative (`%`, `fr`, `auto`, `min-width`) not fixed `px`?
- [ ] **Sidebar Margin:** Not adding `margin-right` for sidebar (handled globally)?
- [ ] **Print:** Does `@media print` hide navigation, sidebar, footer?

---

## 15. Common Anti-Patterns to AVOID

| ❌ Anti-Pattern | ✅ Correct Approach |
|----------------|-------------------|
| `width: 500px` on a container | `max-width: 500px; width: 100%` |
| `position: fixed` without mobile test | Test on 375px; add mobile override if needed |
| Horizontal `d-flex` without `flex-wrap` | `d-flex flex-wrap` or `flex-md-row flex-column` |
| `display: none` in base CSS, show on desktop | Mobile-first: show by default, hide with `d-md-none` |
| `font-size: 24px` on mobile | Use responsive sizing: `fs-4` or clamp |
| New `@media` blocks scattered in view `<style>` | Add to `site.css` in the existing responsive section |
| Table without responsive wrapper | Always `<div class="table-responsive">` or DataTables `responsive: true` |
| Multiple stacked modals on mobile | Use single `#appModal` global modal pattern |
| Updating purchase POS layout without checking sales POS (or vice versa) | Mirror the UX change in the paired module or document why it differs |

---

## 16. CSS File Organization

Responsive rules MUST go in the appropriate CSS file — NOT in inline `<style>` blocks in views:

| CSS File | Responsive Rules For |
|----------|---------------------|
| `site.css` | Global layout, sidebar, cards, modals, appointments, lab |
| `pharmacy-transaction.css` | POS line cards, scanner zone, POS footer |
| `thermal-receipt.css` | Thermal print (80mm) |
| `lab-report-print.css` | Lab report print |

**When adding a new responsive rule:**
1. Open the relevant CSS file.
2. Find or create a `@media` block for the target breakpoint.
3. Add your rule there — grouped with related rules.
4. NEVER duplicate an existing media query breakpoint in the same file.
