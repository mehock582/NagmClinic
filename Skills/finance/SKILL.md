# SKILL: Finance, Pharmacy & Revenue Cycle Patterns

## Trigger
Activate when working on Appointments (billing), Pharmacy Sales, Pharmacy Purchases, Inventory, or any revenue-related feature.

---

## 1. Architecture: Thin Controllers → Scoped Services

All financial logic lives in scoped services registered in `Program.cs`. Controllers delegate to services and handle only HTTP concerns.

### Registered Services

| Interface | Implementation | Purpose |
|-----------|---------------|---------|
| `IPharmacySalesService` | `PharmacySalesService` | Create/Edit/Query pharmacy sales |
| `IPharmacyPurchasesService` | `PharmacyPurchasesService` | Create/Edit/Query pharmacy purchases |
| `IPharmacyStockService` | `PharmacyStockService` | Barcode lookup, FEFO allocation, stock queries, batch management |
| `IPharmacyMasterDataService` | `PharmacyMasterDataService` | CRUD for Categories, Units, Locations, Suppliers |
| `IAppointmentService` | `AppointmentService` | Appointment lifecycle, billing, refunds |

### Controller Pattern (Reference: `PharmacySalesController`)

```csharp
[Authorize(Roles = "Pharmacist,Admin")]
public class PharmacySalesController : BaseController
{
    private readonly IPharmacySalesService _salesService;
    private readonly IPharmacyStockService _stockService;

    // POST Create → delegates to _salesService.ExecuteSaleAsync(model)
    // On failure → ModelState.AddModelError + reload lookups + return View
    // On success → ShowAlert(result.Message) + RedirectToAction(Index)
}
```

**NEVER put price calculation, stock deduction, or batch allocation logic in a controller.**

---

## 2. ServiceResult Pattern

Use `ServiceResult` / `ServiceResult<T>` (namespace `NagmClinic.ViewModels`) for service return types:

```csharp
public class ServiceResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public static ServiceResult SuccessResult(string message = "") => ...;
    public static ServiceResult Error(string message) => ...;
}
```

For stock operations, use specialized result classes:
- `SaleExecutionResult` — `bool Success`, `string Message`, `int? SaleId`
- `PurchaseExecutionResult` — `bool Success`, `string Message`, `int? PurchaseId`
- `FefoAllocationResult` — `bool Success`, `string Message`, `decimal AvailableQuantity`, `List<FefoAllocationLine> Allocations`

---

## 3. Inventory: FEFO (First Expiry, First Out)

When selling, the system uses FEFO allocation — oldest expiry batches are sold first.

### Barcode Lookup Flow (`PharmacySalesController.LookupByBarcode`)

1. Query `ItemBatches` WHERE `Barcode == scannedBarcode` AND `Item.IsActive`, ordered by `ExpiryDate`.
2. If no batches found → check `PharmacyItems` master table:
   - Not found → return `{ status: "NOT_FOUND" }`
   - Found but zero stock → return `{ status: "OUT_OF_STOCK" }`
3. Filter to sellable batches: `QuantityRemaining > 0 && ExpiryDate >= DateTime.Today`
4. If 1 batch → auto-populate the row (`multiMatch: false`)
5. If N batches → force pharmacist to pick via modal (`multiMatch: true`)

### Stock Deduction

`IPharmacyStockService.ExecuteSaleAsync()` handles:
- Batch selection (manual or FEFO-based)
- `QuantityRemaining` decrements on `ItemBatch`
- Line-level `UnitPrice`, `LineTotal` calculation
- Sale `TotalAmount` aggregation
- All within `CreateExecutionStrategy().ExecuteAsync()` transaction

### Purchase Intake

`IPharmacyStockService.ExecutePurchaseAsync()` handles:
- Creating/updating `ItemBatch` records
- `QuantityReceived`, `BonusQuantity`, `QuantityRemaining` increments
- Batch number and barcode generation via `GenerateBatchNumberAsync()` / `GenerateBarcodeAsync()`

---

## 4. Appointment Billing (Revenue Cycle)

### Appointment Model

```
Appointment
  ├── PatientId, DoctorId
  ├── ConsultationFee (decimal)
  ├── Status: Confirmed | Cancelled
  ├── ZeroFeeReason (nullable - required when fee is 0)
  └── AppointmentItems[]
        ├── ServiceId (→ ClinicService)
        ├── UnitPrice, Quantity, TotalPrice
        └── PaymentStatus: Pending | Paid | Refunded | Waived
```

### Key Business Rules

1. **Zero-fee appointments** require `ZeroFeeReason` — never allow a fee-free visit without explanation.
2. **Cancellation** zeroes out all billable amounts (`ConsultationFee = 0`, all item prices = 0).
3. **Refunds** use `IAppointmentService.RefundAppointmentItemAsync(itemId)` — sets `PaymentStatus = Refunded`, zeroes the price.
4. **DailyNumber** is auto-generated per day (sequential within the same `AppointmentDate.Date`).

### Payment Status Enum

```csharp
public enum PaymentStatus
{
    Pending = 0,
    Paid = 1,
    Refunded = 2,
    Waived = 3
}
```

---

## 5. Pharmacy Sale Status & Voiding

```csharp
public enum PharmacySaleStatus
{
    Completed = 1,
    Voided = 2
}
```

- **Voiding** a sale reverses inventory (`QuantityRemaining` restored on batches).
- NEVER hard-delete a `PharmacySale` record.

---

## 6. Decimal Handling Rules

- All money/quantity properties use `decimal`.
- All are configured as `decimal(18,2)` in `OnModelCreating`.
- When calculating totals: `LineTotal = Quantity * UnitPrice`. Aggregate with `Lines.Sum(l => l.LineTotal)`.
- Do NOT use `double` or `float` for any financial value.

---

## 7. DataTables Server-Side Pattern

Financial lists (Sales, Purchases, Appointments) use DataTables.js with server-side processing:

```csharp
// Controller
[HttpPost]
public async Task<IActionResult> GetSalesData()
{
    var dtParams = Request.GetDataTablesParameters(); // Extension method
    var response = await _salesService.GetSalesDataAsync(dtParams);
    return Json(response);
}
```

The `DataTablesParameters` / `DataTablesResponse<T>` classes live in `NagmClinic.Models.DataTables`.

---

## 8. Authorization by Role

| Role | Access |
|------|--------|
| `Admin` | Everything |
| `Pharmacist` | Pharmacy Sales, Purchases, Inventory, Items, Categories, Units, Locations, Suppliers |
| `Cashier` | Appointments, UpdateStatus, Refunds |
| `Doctor` | Appointments (view) |
| `LabTech` | Laboratory |

Applied via `[Authorize(Roles = "Pharmacist,Admin")]` on controller class.

---

## 9. ViewModel Pattern for Create/Edit

Financial forms use dedicated ViewModels:

- `PharmacySaleCreateViewModel` — for new sales (includes `ItemLookup` for dropdowns)
- `PharmacySaleEditViewModel` — for edits (includes `RowVersion` for concurrency)
- `PharmacyPurchaseCreateViewModel` / `PharmacyPurchaseEditViewModel` — same pattern
- `AppointmentCreateViewModel` — shared for both create and edit

Services expose `BuildCreateViewModelAsync()` to populate dropdowns/lookups. On POST validation failure, reload lookups before returning the view:

```csharp
if (!ModelState.IsValid)
{
    await ReloadLookupsAsync(model);
    return View(model);
}
```
