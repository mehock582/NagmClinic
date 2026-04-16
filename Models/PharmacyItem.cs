using System.ComponentModel.DataAnnotations;

namespace NagmClinic.Models
{
    public class PharmacyItem
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم الصنف مطلوب")]
        [MaxLength(200)]
        [Display(Name = "اسم الصنف")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(200)]
        [Display(Name = "الاسم العلمي")]
        public string? GenericName { get; set; }

        [MaxLength(100)]
        [Display(Name = "باركود الصنف")]
        public string? Barcode { get; set; }

        [Required(ErrorMessage = "الوحدة مطلوبة")]
        [Display(Name = "الوحدة")]
        public int UnitId { get; set; }
        public virtual PharmacyUnit? Unit { get; set; }

        [Required(ErrorMessage = "التصنيف مطلوب")]
        [Display(Name = "التصنيف")]
        public int CategoryId { get; set; }
        public virtual PharmacyCategory? Category { get; set; }

        [Required(ErrorMessage = "موقع التخزين مطلوب")]
        [Display(Name = "موقع التخزين")]
        public int LocationId { get; set; }
        public virtual PharmacyLocation? Location { get; set; }

        [Display(Name = "سعر البيع الافتراضي")]
        public decimal DefaultSellingPrice { get; set; }

        [Display(Name = "حد إعادة الطلب")]
        public decimal ReorderLevel { get; set; } = 0;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual ICollection<ItemBatch> Batches { get; set; } = new List<ItemBatch>();
        public virtual ICollection<PharmacyPurchaseLine> PurchaseLines { get; set; } = new List<PharmacyPurchaseLine>();
        public virtual ICollection<PharmacySaleLine> SaleLines { get; set; } = new List<PharmacySaleLine>();
    }
}
