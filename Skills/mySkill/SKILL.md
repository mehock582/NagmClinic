# NagmClinic ERP: AI Agent Directives

## 1. Project Context
* **Stack:** ASP.NET Core MVC, Entity Framework Core, SQL Server.
* **Domain:** Medical Clinic, Pharmacy Inventory, and Revenue Cycle Management (RCM).
* **Language/UI:** Arabic strictly. RTL (Right-to-Left) layout using Bootstrap 5.

## 2. Database & Entity Framework Rules
* **Auditing:** All domain models MUST inherit from `BaseEntity` (handles `CreatedAt`, `UpdatedAt`). Do not manually set timestamps in controllers/services.
* **Concurrency:** Financial and Inventory tables (`PharmacySale`, `PharmacyPurchase`, `Appointment`) MUST use `byte[] RowVersion` `[Timestamp]` for optimistic concurrency.
* **Transactions:** Because `.EnableRetryOnFailure()` is active, NEVER use standard `BeginTransactionAsync`. You MUST wrap transactions in `_context.Database.CreateExecutionStrategy().ExecuteAsync(...)`.
* **Deletions:** NEVER hard-delete Users, Financial records, or Inventory batches. Use Soft Deletes (e.g., `IsActive = false`) or Identity Lockouts.

## 3. Business Logic & Architecture
* **Controller Diet:** Controllers must remain thin. Push complex logic (especially inventory deltas and price calculations) into scoped Services (e.g., `PharmacyStockService`).
* **Lab Integrations:** API endpoints receiving data from external lab hardware must use `[IgnoreAntiforgeryToken]` and authenticate via `X-Connector-Api-Key` header.
* **RCM Enforcement:** Unpaid items must block receipt generation. Always check `TotalBalanceDue` or `IsPaid` flags before finalizing financial actions.

## 4. UI/UX Standards
* **Forms:** Wrap all forms in standard Bootstrap cards (`card shadow-sm`). Inputs must use `form-control`.
* **Validation:** All Identity and DataAnnotation error messages MUST be in Arabic.
* **Tables:** Use DataTables.js for grid layouts. Standard classes: `table table-hover align-middle`.
* **Double-Click Prevention:** Rely on the global `site.js` form submission disabler. Ensure conditional checks exist so simple buttons don't crash if jQuery Validation is absent.
