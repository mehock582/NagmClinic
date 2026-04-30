# SKILL: Database & Entity Framework Core Patterns

## Trigger
Activate when creating/modifying EF Core models, migrations, DbContext configuration, or any database-related code.

---

## 1. Auditing via `BaseEntity`

All domain models MUST inherit from `BaseEntity` (namespace `NagmClinic.Models`), which implements `IAuditableEntity`:

```csharp
// Interfaces/IAuditableEntity.cs
public interface IAuditableEntity
{
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

// Models/BaseEntity.cs
public abstract class BaseEntity : IAuditableEntity
{
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

**Rules:**
- NEVER manually set `CreatedAt` or `UpdatedAt` in controllers or services.
- `ApplicationDbContext.SaveChangesAsync()` automatically applies timestamps via `ApplyAuditTimestamps()`:
  - On `Added` → sets both `CreatedAt` and `UpdatedAt` to `DateTime.UtcNow`.
  - On `Modified` → sets `UpdatedAt` only; marks `CreatedAt` as `IsModified = false` to prevent overwrite.

**Exception:** Some older models (`Appointment`, `PharmacyPurchase`) still have their own `CreatedAt` and do NOT inherit `BaseEntity`. Do not refactor these unless explicitly asked—just follow whichever pattern the model already uses.

---

## 2. Optimistic Concurrency

Financial and inventory models use `[Timestamp]` for optimistic concurrency control:

```csharp
[Timestamp]
public byte[] RowVersion { get; set; } = null!;
```

**Models that already have `RowVersion`:**
- `PharmacySale`
- `PharmacyPurchase`
- `ItemBatch`

**Handling concurrency conflicts:**
- Services throw `ConcurrencyException` (namespace `NagmClinic.Exceptions`).
- `BaseController.OnActionExecuted()` catches `ConcurrencyException`, calls `ShowAlert()`, and redirects to `Index`.
- When editing, always load and pass `RowVersion` into the ViewModel and include it as a hidden field in the form.

```csharp
// In service code, catch DbUpdateConcurrencyException:
catch (DbUpdateConcurrencyException)
{
    throw new ConcurrencyException("تم تعديل هذا السجل من مستخدم آخر. يرجى إعادة المحاولة.");
}
```

---

## 3. Execution Strategy for Transactions

`EnableRetryOnFailure()` is configured in `Program.cs`:
```csharp
options.UseSqlServer(connectionString, sqlServerOptions => sqlServerOptions.EnableRetryOnFailure());
```

**CRITICAL:** Because of retry-on-failure, you CANNOT use `_context.Database.BeginTransactionAsync()` directly. You MUST wrap multi-statement transactions in:

```csharp
var strategy = _context.Database.CreateExecutionStrategy();
await strategy.ExecuteAsync(async () =>
{
    using var transaction = await _context.Database.BeginTransactionAsync();
    try
    {
        // ... all your DB operations ...
        await _context.SaveChangesAsync();
        await transaction.CommitAsync();
    }
    catch
    {
        await transaction.RollbackAsync();
        throw;
    }
});
```

---

## 4. Soft Deletes

- **Patients:** Use `IsDeleted` flag. A global query filter is applied:
  ```csharp
  builder.Entity<Patient>().HasQueryFilter(p => !p.IsDeleted);
  ```
- **Users:** Use Identity Lockout (`SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue)`). NEVER hard-delete users.
- **Pharmacy Items:** Use `IsActive` flag. Filter with `.Where(i => i.IsActive)` in queries.
- **Financial records** (`PharmacySale`, `PharmacyPurchase`, `ItemBatch`): NEVER hard-delete. Use status changes (e.g., `PharmacySaleStatus.Voided`).

---

## 5. Audit Trail

`ApplicationDbContext` automatically logs INSERT/UPDATE/DELETE operations to `AuditLogs` table via `OnBeforeSaveChanges()` / `OnAfterSaveChanges()`. This captures:
- `UserId` (from `HttpContext`)
- `TableName`, `AuditType` ("Insert", "Update", "Delete")
- `OldValues` / `NewValues` as serialized JSON

No manual audit code is needed in services or controllers.

---

## 6. Decimal Precision

All currency and quantity columns MUST be configured as `decimal(18,2)` in `OnModelCreating`:

```csharp
builder.Entity<YourModel>().Property(x => x.PriceField).HasColumnType("decimal(18,2)");
```

Already configured for: `ClinicService.Price`, `AppointmentItem.UnitPrice/TotalPrice`, `Doctor.ConsultationFee`, `Appointment.ConsultationFee`, all `ItemBatch` prices/quantities, all `PharmacyPurchaseLine` columns, all `PharmacySaleLine` columns.

**When adding a new decimal property**, always add the matching Fluent API config.

---

## 7. Relationship Conventions

| Pattern | Example |
|---------|---------|
| Restrict on delete (lookups) | `PharmacyItem → Unit`, `PharmacyItem → Category`, `PharmacyItem → Location` |
| Cascade on delete (children) | `PharmacyPurchase → Lines`, `PharmacySale → Lines` |
| SetNull on delete (optional) | `LabResultImportRecord → LabResult` |

Always specify `OnDelete()` explicitly in Fluent API—do not rely on EF defaults.

---

## 8. Index Conventions

- Unique indexes with `HasFilter("[Column] IS NOT NULL")` for nullable unique columns.
- Composite unique indexes for natural keys: `(ItemId, BatchNumber)`, `(DeviceId, DeviceTestCode)`.
- Non-unique indexes for frequently filtered/searched columns: `Barcode`, `ImportedAt`.

---

## 9. DbSet Naming

Follow singular entity name → plural DbSet:
```csharp
public DbSet<Patient> Patients { get; set; }
public DbSet<ItemBatch> ItemBatches { get; set; }
```

When adding a new entity, add its `DbSet<T>` to `ApplicationDbContext` immediately.
