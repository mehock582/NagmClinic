using System.ComponentModel.DataAnnotations;

namespace NagmClinic.Models
{
    public class PharmacyPurchaseLine : BaseEntity
    {
        public int Id { get; set; }

        [Required]
        public int PurchaseId { get; set; }
        public virtual PharmacyPurchase? Purchase { get; set; }

        [Required]
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

        [Display(Name = "الكمية")]
        public decimal Quantity { get; set; }

        [Display(Name = "البونص")]
        public decimal BonusQuantity { get; set; }

        [Display(Name = "سعر الشراء")]
        public decimal PurchasePrice { get; set; }

        [Display(Name = "سعر البيع")]
        public decimal SellingPrice { get; set; }

        [Display(Name = "إجمالي السطر")]
        public decimal LineTotal { get; set; }

        public int? ItemBatchId { get; set; }
        public virtual ItemBatch? ItemBatch { get; set; }
    }
}
