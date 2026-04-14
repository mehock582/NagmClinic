using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NagmClinic.Services.Laboratory
{
    public class LabResultsImportRequest
    {
        [MaxLength(100)]
        public string ConnectorSource { get; set; } = string.Empty;

        [Required]
        public List<NormalizedLabResultItem> Results { get; set; } = new();
    }

    public class NormalizedLabResultItem
    {
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

        public string? RawPayload { get; set; }
    }

    public class LabResultsImportResponse
    {
        public int Total { get; set; }
        public int Imported { get; set; }
        public int Duplicates { get; set; }
        public int Rejected { get; set; }
        public List<LabImportItemOutcome> Items { get; set; } = new();
    }

    public class LabImportItemOutcome
    {
        public string DeviceId { get; set; } = string.Empty;
        public string PatientIdentifier { get; set; } = string.Empty;
        public string TestCode { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? Message { get; set; }
    }
}
