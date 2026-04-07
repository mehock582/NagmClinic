using System.ComponentModel.DataAnnotations;

namespace NagmClinic.Models
{
    public class ItemBatch
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "الصنف مطلوب")]
        public int ItemId { get; set; }
        public virtual PharmacyItem? Item { get; set; }

        [Required(ErrorMessage = "رقم الباتش مطلوب")]
        [MaxLength(100)]
        [Display(Name = "رقم الباتش")]
        public string BatchNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "الباركود مطلوب")]
        [MaxLength(100)]
        [Display(Name = "الباركود")]
        public string Barcode { get; set; } = string.Empty;

        [Required(ErrorMessage = "تاريخ الانتهاء مطلوب")]
        [Display(Name = "تاريخ الانتهاء")]
        public DateTime ExpiryDate { get; set; }

        [Display(Name = "كمية الشراء")]
        public decimal QuantityReceived { get; set; }

        [Display(Name = "كمية البونص")]
        public decimal BonusQuantity { get; set; }

        [Display(Name = "الكمية المتبقية")]
        public decimal QuantityRemaining { get; set; }

        [Display(Name = "سعر الشراء")]
        public decimal PurchasePrice { get; set; }

        [Display(Name = "سعر البيع")]
        public decimal SellingPrice { get; set; }

        public int? SupplierId { get; set; }
        public virtual PharmacySupplier? Supplier { get; set; }

        public DateTime ReceivedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;

        public virtual ICollection<PharmacySaleLine> SaleLines { get; set; } = new List<PharmacySaleLine>();
    }
}
