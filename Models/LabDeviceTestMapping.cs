using System;
using System.ComponentModel.DataAnnotations;

namespace NagmClinic.Models
{
    public class LabDeviceTestMapping
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string DeviceId { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string DeviceTestCode { get; set; } = string.Empty;

        public int LabTestId { get; set; }
        public virtual ClinicService LabTest { get; set; } = default!;

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }
    }
}
