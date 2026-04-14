using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NagmClinic.Models
{
    public class LabAnalyzer
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Code { get; set; }

        [MaxLength(200)]
        public string? Manufacturer { get; set; }

        [MaxLength(100)]
        public string? WholeBloodSampleVolume { get; set; }

        [MaxLength(100)]
        public string? PredilutedSampleVolume { get; set; }

        public string? Notes { get; set; }

        public bool IsActive { get; set; } = true;

        public virtual ICollection<ClinicService> Tests { get; set; } = new List<ClinicService>();
    }
}
