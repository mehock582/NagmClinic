using System.ComponentModel.DataAnnotations;

namespace NagmClinic.Models
{
    public class PharmacyLocation
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "كود الموقع مطلوب")]
        [MaxLength(50)]
        [Display(Name = "كود الموقع")]
        public string Code { get; set; } = string.Empty;

        [Required(ErrorMessage = "وصف الموقع مطلوب")]
        [MaxLength(300)]
        [Display(Name = "وصف الموقع")]
        public string Description { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual ICollection<PharmacyItem> Items { get; set; } = new List<PharmacyItem>();
    }
}
