using System.ComponentModel.DataAnnotations;
using NagmClinic.Models.Enums;

namespace NagmClinic.Models
{
    public class PharmacySale
    {
        public int Id { get; set; }

        [Display(Name = "تاريخ البيع")]
        public DateTime SaleDate { get; set; } = DateTime.Now;

        [MaxLength(200)]
        [Display(Name = "اسم العميل")]
        public string? CustomerName { get; set; }

        [Display(Name = "الإجمالي")]
        public decimal TotalAmount { get; set; }

        [Display(Name = "الحالة")]
        public PharmacySaleStatus Status { get; set; } = PharmacySaleStatus.Completed;

        [MaxLength(500)]
        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual ICollection<PharmacySaleLine> Lines { get; set; } = new List<PharmacySaleLine>();
    }
}
