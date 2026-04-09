using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NagmClinic.ViewModels
{
    public class PharmacyUnitViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم الوحدة مطلوب")]
        [Display(Name = "اسم الوحدة")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "الاسم الإنجليزي")]
        public string? NameEn { get; set; }

        [Display(Name = "الحالة")]
        public bool IsActive { get; set; } = true;
    }

    public class PharmacyCategoryViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم التصنيف مطلوب")]
        [Display(Name = "اسم التصنيف")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "الوصف")]
        public string? Description { get; set; }

        [Display(Name = "الحالة")]
        public bool IsActive { get; set; } = true;
    }

    public class PharmacySupplierViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم المورد مطلوب")]
        [Display(Name = "اسم المورد")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "شخص التواصل")]
        public string? ContactPerson { get; set; }

        [Display(Name = "الهاتف")]
        public string? PhoneNumber { get; set; }

        [Display(Name = "العنوان")]
        public string? Address { get; set; }

        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        [Display(Name = "الحالة")]
        public bool IsActive { get; set; } = true;
    }

    public class PharmacyLocationViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "كود الموقع مطلوب")]
        [Display(Name = "الكود")]
        public string Code { get; set; } = string.Empty;

        [Required(ErrorMessage = "وصف الموقع مطلوب")]
        [Display(Name = "الوصف")]
        public string Description { get; set; } = string.Empty;

        [Display(Name = "الحالة")]
        public bool IsActive { get; set; } = true;
    }

    public class PharmacyItemViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم الصنف مطلوب")]
        [Display(Name = "اسم الصنف")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "الاسم العلمي")]
        public string? GenericName { get; set; }

        [Required(ErrorMessage = "الوحدة مطلوبة")]
        [Display(Name = "الوحدة")]
        public int UnitId { get; set; }

        [Required(ErrorMessage = "التصنيف مطلوب")]
        [Display(Name = "التصنيف")]
        public int CategoryId { get; set; }

        [Required(ErrorMessage = "موقع التخزين مطلوب")]
        [Display(Name = "الموقع")]
        public int LocationId { get; set; }

        [Display(Name = "سعر البيع الافتراضي")]
        [Range(0, 999999, ErrorMessage = "سعر البيع غير صحيح")]
        public decimal DefaultSellingPrice { get; set; }

        [Display(Name = "حد إعادة الطلب")]
        [Range(0, 999999, ErrorMessage = "قيمة حد إعادة الطلب غير صحيحة")]
        public decimal ReorderLevel { get; set; }

        [Display(Name = "الحالة")]
        public bool IsActive { get; set; } = true;
    }

    public class PharmacyItemIndexViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? GenericName { get; set; }
        public string UnitName { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
        public int LocationId { get; set; }
        public string LocationCode { get; set; } = string.Empty;
        public decimal DefaultSellingPrice { get; set; }
        public decimal ReorderLevel { get; set; }
        public decimal AvailableStock { get; set; }
        public bool IsActive { get; set; }
    }

    public class PharmacyPurchaseCreateViewModel
    {
        [Range(1, int.MaxValue, ErrorMessage = "المورد مطلوب")]
        [Display(Name = "المورد")]
        public int SupplierId { get; set; }

        [Display(Name = "رقم الفاتورة")]
        public string? InvoiceNumber { get; set; }

        [Required(ErrorMessage = "تاريخ الشراء مطلوب")]
        [Display(Name = "تاريخ الشراء")]
        public DateTime PurchaseDate { get; set; } = DateTime.Now;

        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        public List<PharmacyPurchaseLineInputViewModel> Lines { get; set; } = new();

        // Lookup data for dropdowns (replaces ViewBag.Suppliers / ViewBag.Items)
        public object? SupplierLookup { get; set; }
        public object? ItemLookup { get; set; }
    }

    public class PharmacyPurchaseLineInputViewModel : IValidatableObject
    {
        [Range(1, int.MaxValue, ErrorMessage = "يرجى اختيار الصنف")]
        public int ItemId { get; set; }

        [Required(ErrorMessage = "رقم الباتش مطلوب")]
        public string BatchNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "تاريخ الانتهاء مطلوب")]
        public DateTime ExpiryDate { get; set; }

        [Required(ErrorMessage = "الباركود مطلوب")]
        public string Barcode { get; set; } = string.Empty;

        [Range(0.01, 999999, ErrorMessage = "الكمية يجب أن تكون أكبر من صفر")]
        public decimal Quantity { get; set; }

        [Range(0.01, 999999, ErrorMessage = "سعر الشراء يجب أن يكون أكبر من صفر")]
        public decimal PurchasePrice { get; set; }

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            if (ExpiryDate == default)
            {
                yield return new ValidationResult("تاريخ الانتهاء مطلوب", new[] { nameof(ExpiryDate) });
            }
        }
    }

    public class PharmacySaleCreateViewModel
    {
        [Display(Name = "تاريخ البيع")]
        public DateTime SaleDate { get; set; } = DateTime.Now;

        [Display(Name = "اسم العميل")]
        public string? CustomerName { get; set; }

        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        public List<PharmacySaleLineInputViewModel> Lines { get; set; } = new();

        // Lookup data for item dropdown (replaces ViewBag.Items)
        public object? ItemLookup { get; set; }
    }

    public class PharmacySaleLineInputViewModel
    {
        [Range(1, int.MaxValue, ErrorMessage = "يرجى اختيار الصنف")]
        public int ItemId { get; set; }

        [Range(0.01, 999999, ErrorMessage = "الكمية يجب أن تكون أكبر من صفر")]
        public decimal Quantity { get; set; }

        [Range(0.01, 999999, ErrorMessage = "سعر البيع يجب أن يكون أكبر من صفر")]
        public decimal SellingPrice { get; set; }
    }

    public class PharmacyInventoryItemViewModel
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public string UnitName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public string SlotCode { get; set; } = string.Empty;
        public decimal AvailableQuantity { get; set; }
        public decimal ReorderLevel { get; set; }
        public bool IsLowStock => AvailableQuantity <= ReorderLevel;
    }

    public class PharmacyInventoryBatchViewModel
    {
        public string ItemName { get; set; } = string.Empty;
        public string BatchNumber { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public DateTime ExpiryDate { get; set; }
        public decimal QuantityRemaining { get; set; }
        public decimal PurchasePrice { get; set; }
        public decimal SellingPrice { get; set; }
        public string SupplierName { get; set; } = "-";
        public bool IsExpired => ExpiryDate.Date < DateTime.Today;
    }

    public class PharmacyInventoryPageViewModel
    {
        public List<PharmacyInventoryItemViewModel> Items { get; set; } = new();
        public List<PharmacyInventoryBatchViewModel> Batches { get; set; } = new();
    }

    public class PharmacyPurchaseIndexViewModel
    {
        public int Id { get; set; }
        public string? InvoiceNumber { get; set; }
        public DateTime PurchaseDate { get; set; }
        public string SupplierName { get; set; } = "-";
        public decimal TotalAmount { get; set; }
        public int LinesCount { get; set; }
    }

    public class PharmacySaleIndexViewModel
    {
        public int Id { get; set; }
        public DateTime SaleDate { get; set; }
        public string? CustomerName { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = string.Empty;
        public int LinesCount { get; set; }
    }
}



