using System.ComponentModel.DataAnnotations;

namespace NagmClinic.Models
{
    public class PharmacyUnit
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم الوحدة مطلوب")]
        [MaxLength(100)]
        [Display(Name = "اسم الوحدة")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(100)]
        [Display(Name = "الاسم الإنجليزي")]
        public string? NameEn { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual ICollection<PharmacyItem> Items { get; set; } = new List<PharmacyItem>();
    }
}
