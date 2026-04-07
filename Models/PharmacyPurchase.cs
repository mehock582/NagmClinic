using System.ComponentModel.DataAnnotations;

namespace NagmClinic.Models
{
    public class PharmacyPurchase
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "المورد مطلوب")]
        [Display(Name = "المورد")]
        public int SupplierId { get; set; }
        public virtual PharmacySupplier? Supplier { get; set; }

        [MaxLength(100)]
        [Display(Name = "رقم الفاتورة")]
        public string? InvoiceNumber { get; set; }

        [Display(Name = "تاريخ الشراء")]
        public DateTime PurchaseDate { get; set; } = DateTime.Now;

        [Display(Name = "إجمالي الفاتورة")]
        public decimal TotalAmount { get; set; }

        [MaxLength(500)]
        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual ICollection<PharmacyPurchaseLine> Lines { get; set; } = new List<PharmacyPurchaseLine>();
    }
}
