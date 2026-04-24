using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NagmClinic.Models;
using NagmClinic.Interfaces;

namespace NagmClinic.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        private readonly Microsoft.AspNetCore.Http.IHttpContextAccessor _httpContextAccessor;

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, Microsoft.AspNetCore.Http.IHttpContextAccessor httpContextAccessor)
            : base(options)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<Patient> Patients { get; set; }
        public DbSet<Doctor> Doctors { get; set; }
        public DbSet<ClinicService> ClinicServices { get; set; }
        public DbSet<Appointment> Appointments { get; set; }
        public DbSet<AppointmentItem> AppointmentItems { get; set; }
        public DbSet<LabResult> LabResults { get; set; }
        public DbSet<LabCategory> LabCategories { get; set; }
        public DbSet<LabAnalyzer> LabAnalyzers { get; set; }
        public DbSet<LabDeviceTestMapping> LabDeviceTestMappings { get; set; }
        public DbSet<LabResultImportRecord> LabResultImportRecords { get; set; }
        public DbSet<PharmacyUnit> PharmacyUnits { get; set; }
        public DbSet<PharmacyCategory> PharmacyCategories { get; set; }
        public DbSet<PharmacySupplier> PharmacySuppliers { get; set; }
        public DbSet<PharmacyLocation> PharmacyLocations { get; set; }
        public DbSet<PharmacyItem> PharmacyItems { get; set; }
        public DbSet<ItemBatch> ItemBatches { get; set; }
        public DbSet<PharmacyPurchase> PharmacyPurchases { get; set; }
        public DbSet<PharmacyPurchaseLine> PharmacyPurchaseLines { get; set; }
        public DbSet<PharmacySale> PharmacySales { get; set; }
        public DbSet<PharmacySaleLine> PharmacySaleLines { get; set; }

        public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            ApplyAuditTimestamps();
            var auditEntries = OnBeforeSaveChanges();
            var result = await base.SaveChangesAsync(cancellationToken);
            await OnAfterSaveChanges(auditEntries);
            return result;
        }

        private void ApplyAuditTimestamps()
        {
            var entries = ChangeTracker.Entries<IAuditableEntity>();
            foreach (var entry in entries)
            {
                if (entry.State == EntityState.Added)
                {
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                }
                else if (entry.State == EntityState.Modified)
                {
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    // Ensure EF doesn't accidentally overwrite the original CreatedAt date
                    entry.Property(p => p.CreatedAt).IsModified = false;
                }
            }
        }

        private List<AuditEntry> OnBeforeSaveChanges()
        {
            ChangeTracker.DetectChanges();
            var auditEntries = new List<AuditEntry>();
            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.Entity is AuditLog || entry.State == EntityState.Detached || entry.State == EntityState.Unchanged)
                    continue;

                var auditEntry = new AuditEntry(entry);
                auditEntry.TableName = entry.Entity.GetType().Name;
                auditEntry.UserId = _httpContextAccessor.HttpContext?.User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                auditEntries.Add(auditEntry);

                foreach (var property in entry.Properties)
                {
                    string propertyName = property.Metadata.Name;
                    if (property.Metadata.IsPrimaryKey())
                    {
                        auditEntry.KeyValues[propertyName] = property.CurrentValue;
                        continue;
                    }

                    if (property.IsTemporary)
                    {
                        auditEntry.TemporaryProperties.Add(property);
                        continue;
                    }

                    switch (entry.State)
                    {
                        case EntityState.Added:
                            auditEntry.AuditType = "Insert";
                            auditEntry.NewValues[propertyName] = property.CurrentValue;
                            break;

                        case EntityState.Deleted:
                            auditEntry.AuditType = "Delete";
                            auditEntry.OldValues[propertyName] = property.OriginalValue;
                            break;

                        case EntityState.Modified:
                            if (property.IsModified)
                            {
                                auditEntry.AuditType = "Update";
                                auditEntry.OldValues[propertyName] = property.OriginalValue;
                                auditEntry.NewValues[propertyName] = property.CurrentValue;
                            }
                            break;
                    }
                }
            }

            // Save audit entries that have no temporary properties
            foreach (var auditEntry in auditEntries.Where(_ => !_.HasTemporaryProperties))
            {
                AuditLogs.Add(auditEntry.ToAudit());
            }

            // Return audit entries that have temporary properties (like generated IDs)
            return auditEntries.Where(_ => _.HasTemporaryProperties).ToList();
        }

        private Task OnAfterSaveChanges(List<AuditEntry> auditEntries)
        {
            if (auditEntries == null || auditEntries.Count == 0)
                return Task.CompletedTask;

            foreach (var auditEntry in auditEntries)
            {
                // Get the generated ID
                foreach (var prop in auditEntry.TemporaryProperties)
                {
                    if (prop.Metadata.IsPrimaryKey())
                    {
                        auditEntry.KeyValues[prop.Metadata.Name] = prop.CurrentValue;
                    }
                    else
                    {
                        auditEntry.NewValues[prop.Metadata.Name] = prop.CurrentValue;
                    }
                }
                AuditLogs.Add(auditEntry.ToAudit());
            }
            return base.SaveChangesAsync();
        }

        private class AuditEntry
        {
            public AuditEntry(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry)
            {
                Entry = entry;
            }
            public Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry Entry { get; }
            public string? UserId { get; set; }
            public string TableName { get; set; } = string.Empty;
            public Dictionary<string, object?> KeyValues { get; } = new();
            public Dictionary<string, object?> OldValues { get; } = new();
            public Dictionary<string, object?> NewValues { get; } = new();
            public string AuditType { get; set; } = string.Empty;
            public List<Microsoft.EntityFrameworkCore.ChangeTracking.PropertyEntry> TemporaryProperties { get; } = new();

            public bool HasTemporaryProperties => TemporaryProperties.Any();

            public AuditLog ToAudit()
            {
                var audit = new AuditLog();
                audit.UserId = UserId;
                audit.Action = AuditType;
                audit.TableName = TableName;
                audit.Timestamp = DateTime.Now;
                audit.OldValues = OldValues.Count == 0 ? null : System.Text.Json.JsonSerializer.Serialize(OldValues);
                audit.NewValues = NewValues.Count == 0 ? null : System.Text.Json.JsonSerializer.Serialize(NewValues);
                return audit;
            }
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<Patient>().HasQueryFilter(p => !p.IsDeleted);

            // Configure Appointment relationship
            builder.Entity<Appointment>()
                .HasOne(a => a.Patient)
                .WithMany(p => p.Appointments)
                .HasForeignKey(a => a.PatientId);

            builder.Entity<Appointment>()
                .HasOne(a => a.Doctor)
                .WithMany(d => d.Appointments)
                .HasForeignKey(a => a.DoctorId);

            builder.Entity<AppointmentItem>()
                .HasOne(ai => ai.Appointment)
                .WithMany(a => a.AppointmentItems)
                .HasForeignKey(ai => ai.AppointmentId);

            builder.Entity<LabResult>()
                .HasOne(lr => lr.AppointmentItem)
                .WithOne(ai => ai.LabResult)
                .HasForeignKey<LabResult>(lr => lr.AppointmentItemId);

            builder.Entity<LabResultImportRecord>()
                .HasOne(r => r.LabResult)
                .WithMany()
                .HasForeignKey(r => r.LabResultId)
                .OnDelete(DeleteBehavior.SetNull);

            builder.Entity<LabDeviceTestMapping>()
                .HasOne(m => m.LabTest)
                .WithMany()
                .HasForeignKey(m => m.LabTestId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ClinicService>()
                .HasIndex(c => c.Code)
                .IsUnique()
                .HasFilter("[Code] IS NOT NULL");

            builder.Entity<ClinicService>()
                .HasIndex(c => c.DeviceCode)
                .HasFilter("[DeviceCode] IS NOT NULL");

            builder.Entity<LabDeviceTestMapping>()
                .HasIndex(m => new { m.DeviceId, m.DeviceTestCode })
                .IsUnique();

            builder.Entity<LabResultImportRecord>()
                .HasIndex(r => new { r.DeviceId, r.TestCode, r.PatientIdentifier, r.Timestamp })
                .IsUnique();

            builder.Entity<LabResultImportRecord>()
                .HasIndex(r => r.ImportedAt);

            builder.Entity<LabCategory>()
                .HasIndex(c => c.NameAr)
                .IsUnique();

            builder.Entity<LabAnalyzer>()
                .HasIndex(a => a.Code)
                .IsUnique()
                .HasFilter("[Code] IS NOT NULL");

            builder.Entity<ClinicService>()
                .HasOne(c => c.LabCategory)
                .WithMany(c => c.Tests)
                .HasForeignKey(c => c.LabCategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ClinicService>()
                .HasOne(c => c.LabAnalyzer)
                .WithMany(a => a.Tests)
                .HasForeignKey(c => c.LabAnalyzerId)
                .OnDelete(DeleteBehavior.Restrict);

            // Precision for currency values
            builder.Entity<ClinicService>().Property(c => c.Price).HasColumnType("decimal(18,2)");
            builder.Entity<AppointmentItem>().Property(ai => ai.UnitPrice).HasColumnType("decimal(18,2)");
            builder.Entity<AppointmentItem>().Property(ai => ai.TotalPrice).HasColumnType("decimal(18,2)");
            builder.Entity<Doctor>().Property(d => d.ConsultationFee).HasColumnType("decimal(18,2)");
            builder.Entity<Appointment>().Property(a => a.ConsultationFee).HasColumnType("decimal(18,2)");

            builder.Entity<PharmacyLocation>()
                .HasIndex(l => l.Code)
                .IsUnique();

            builder.Entity<PharmacyUnit>()
                .HasIndex(u => u.Name)
                .IsUnique();

            builder.Entity<PharmacyCategory>()
                .HasIndex(c => c.Name)
                .IsUnique();

            builder.Entity<ItemBatch>()
                .HasIndex(b => b.Barcode); // Removed IsUnique() to allow multiple batches sharing manufacturer UPC

            builder.Entity<PharmacyItem>()
                .HasOne(i => i.Unit)
                .WithMany(u => u.Items)
                .HasForeignKey(i => i.UnitId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<PharmacyItem>()
                .HasOne(i => i.Category)
                .WithMany(c => c.Items)
                .HasForeignKey(i => i.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<PharmacyItem>()
                .HasOne(i => i.Location)
                .WithMany(l => l.Items)
                .HasForeignKey(i => i.LocationId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<PharmacyPurchase>()
                .HasOne(p => p.Supplier)
                .WithMany(s => s.Purchases)
                .HasForeignKey(p => p.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<PharmacyPurchaseLine>()
                .HasOne(l => l.Purchase)
                .WithMany(p => p.Lines)
                .HasForeignKey(l => l.PurchaseId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<PharmacyPurchaseLine>()
                .HasOne(l => l.Item)
                .WithMany(i => i.PurchaseLines)
                .HasForeignKey(l => l.ItemId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ItemBatch>()
                .HasOne(b => b.Item)
                .WithMany(i => i.Batches)
                .HasForeignKey(b => b.ItemId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ItemBatch>()
                .HasOne(b => b.Supplier)
                .WithMany(s => s.ItemBatches)
                .HasForeignKey(b => b.SupplierId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<ItemBatch>()
                .HasIndex(b => new { b.ItemId, b.BatchNumber })
                .IsUnique();

            builder.Entity<ItemBatch>()
                .HasIndex(b => new { b.ItemId, b.ExpiryDate });

            builder.Entity<PharmacySaleLine>()
                .HasOne(l => l.Sale)
                .WithMany(s => s.Lines)
                .HasForeignKey(l => l.SaleId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<PharmacySaleLine>()
                .HasOne(l => l.Item)
                .WithMany(i => i.SaleLines)
                .HasForeignKey(l => l.ItemId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<PharmacySaleLine>()
                .HasOne(l => l.ItemBatch)
                .WithMany(b => b.SaleLines)
                .HasForeignKey(l => l.ItemBatchId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<PharmacyItem>()
                .HasIndex(i => i.Barcode)
                .IsUnique()
                .HasFilter("[Barcode] IS NOT NULL");

            builder.Entity<PharmacyItem>().Property(i => i.DefaultSellingPrice).HasColumnType("decimal(18,2)");
            builder.Entity<PharmacyItem>().Property(i => i.ReorderLevel).HasColumnType("decimal(18,2)");

            builder.Entity<ItemBatch>().Property(b => b.QuantityReceived).HasColumnType("decimal(18,2)");
            builder.Entity<ItemBatch>().Property(b => b.BonusQuantity).HasColumnType("decimal(18,2)");
            builder.Entity<ItemBatch>().Property(b => b.QuantityRemaining).HasColumnType("decimal(18,2)");
            builder.Entity<ItemBatch>().Property(b => b.PurchasePrice).HasColumnType("decimal(18,2)");
            builder.Entity<ItemBatch>().Property(b => b.SellingPrice).HasColumnType("decimal(18,2)");

            builder.Entity<PharmacyPurchase>().Property(p => p.TotalAmount).HasColumnType("decimal(18,2)");
            builder.Entity<PharmacyPurchaseLine>().Property(l => l.Quantity).HasColumnType("decimal(18,2)");
            builder.Entity<PharmacyPurchaseLine>().Property(l => l.BonusQuantity).HasColumnType("decimal(18,2)");
            builder.Entity<PharmacyPurchaseLine>().Property(l => l.PurchasePrice).HasColumnType("decimal(18,2)");
            builder.Entity<PharmacyPurchaseLine>().Property(l => l.SellingPrice).HasColumnType("decimal(18,2)");
            builder.Entity<PharmacyPurchaseLine>().Property(l => l.LineTotal).HasColumnType("decimal(18,2)");

            builder.Entity<PharmacySale>().Property(s => s.TotalAmount).HasColumnType("decimal(18,2)");
            builder.Entity<PharmacySaleLine>().Property(l => l.Quantity).HasColumnType("decimal(18,2)");
            builder.Entity<PharmacySaleLine>().Property(l => l.UnitPrice).HasColumnType("decimal(18,2)");
            builder.Entity<PharmacySaleLine>().Property(l => l.LineTotal).HasColumnType("decimal(18,2)");
        }
    }
}
