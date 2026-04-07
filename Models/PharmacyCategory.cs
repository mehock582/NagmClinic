using System.ComponentModel.DataAnnotations;

namespace NagmClinic.Models
{
    public class PharmacyCategory
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم التصنيف مطلوب")]
        [MaxLength(150)]
        [Display(Name = "اسم التصنيف")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(300)]
        [Display(Name = "الوصف")]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual ICollection<PharmacyItem> Items { get; set; } = new List<PharmacyItem>();
    }
}
