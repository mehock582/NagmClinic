using System;
using System.ComponentModel.DataAnnotations;
using NagmClinic.Models.Enums;

namespace NagmClinic.Models
{
    public class LabResultImportRecord
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string DeviceId { get; set; } = string.Empty;

        [Required]
        [MaxLength(150)]
        public string PatientIdentifier { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string TestCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string ResultValue { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? Unit { get; set; }

        public DateTime Timestamp { get; set; }

        [MaxLength(100)]
        public string ConnectorSource { get; set; } = string.Empty;

        public DateTime ImportedAt { get; set; } = DateTime.UtcNow;

        public LabImportProcessingStatus ProcessingStatus { get; set; } = LabImportProcessingStatus.Pending;

        public int? LabResultId { get; set; }
        public virtual LabResult? LabResult { get; set; }

        public string? ErrorMessage { get; set; }

        public string? RawPayload { get; set; }
    }
}
