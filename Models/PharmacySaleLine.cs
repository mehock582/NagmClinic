using System.ComponentModel.DataAnnotations;

namespace NagmClinic.Models
{
    public class PharmacySaleLine
    {
        public int Id { get; set; }

        [Required]
        public int SaleId { get; set; }
        public virtual PharmacySale? Sale { get; set; }

        [Required]
        public int ItemId { get; set; }
        public virtual PharmacyItem? Item { get; set; }

        [Required]
        public int ItemBatchId { get; set; }
        public virtual ItemBatch? ItemBatch { get; set; }

        [Display(Name = "الكمية")]
        public decimal Quantity { get; set; }

        [Display(Name = "سعر البيع")]
        public decimal UnitPrice { get; set; }

        [Display(Name = "إجمالي السطر")]
        public decimal LineTotal { get; set; }

        [MaxLength(100)]
        public string BatchNumberSnapshot { get; set; } = string.Empty;

        public DateTime ExpiryDateSnapshot { get; set; }

        [MaxLength(50)]
        public string SlotCodeSnapshot { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
