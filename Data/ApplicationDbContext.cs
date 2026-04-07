using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using NagmClinic.Models;

namespace NagmClinic.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Patient> Patients { get; set; }
        public DbSet<Doctor> Doctors { get; set; }
        public DbSet<ClinicService> ClinicServices { get; set; }
        public DbSet<Appointment> Appointments { get; set; }
        public DbSet<AppointmentItem> AppointmentItems { get; set; }
        public DbSet<LabResult> LabResults { get; set; }
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

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

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
                .HasIndex(b => b.Barcode)
                .IsUnique();

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
