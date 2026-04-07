using System.ComponentModel.DataAnnotations;

namespace NagmClinic.Models
{
    public class PharmacySupplier
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم المورد مطلوب")]
        [MaxLength(200)]
        [Display(Name = "اسم المورد")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(100)]
        [Display(Name = "شخص التواصل")]
        public string? ContactPerson { get; set; }

        [MaxLength(50)]
        [Display(Name = "الهاتف")]
        public string? PhoneNumber { get; set; }

        [MaxLength(300)]
        [Display(Name = "العنوان")]
        public string? Address { get; set; }

        [MaxLength(500)]
        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual ICollection<PharmacyPurchase> Purchases { get; set; } = new List<PharmacyPurchase>();
        public virtual ICollection<ItemBatch> ItemBatches { get; set; } = new List<ItemBatch>();
    }
}
