using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NagmClinic.Models
{
    public class LabCategory
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(150)]
        public string NameAr { get; set; } = string.Empty;

        [MaxLength(150)]
        public string? NameEn { get; set; }

        public int SortOrder { get; set; }

        public bool IsActive { get; set; } = true;

        public virtual ICollection<ClinicService> Tests { get; set; } = new List<ClinicService>();
    }
}
