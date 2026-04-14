using System;
using System.ComponentModel.DataAnnotations;
using NagmClinic.Models.Enums;

namespace NagmClinic.Models
{
    public class LabResult
    {
        public int Id { get; set; }

        public int AppointmentItemId { get; set; }
        public virtual AppointmentItem AppointmentItem { get; set; } = default!;

        [Display(Name = "النتيجة")]
        public string? ResultValue { get; set; }

        [Display(Name = "الوحدة")]
        public string? Unit { get; set; }

        [Display(Name = "المدى الطبيعي")]
        public string? NormalRange { get; set; }

        [Display(Name = "الحالة")]
        public LabStatus Status { get; set; } = LabStatus.Pending;

        [Display(Name = "تم الفحص بواسطة")]
        public string? PerformedBy { get; set; }

        [Display(Name = "تاريخ الفحص")]
        public DateTime? PerformedAt { get; set; }

        [Display(Name = "تاريخ الطلب")]
        public DateTime RequestedAt { get; set; } = DateTime.Now;

        [Display(Name = "ملاحظات الفني")]
        public string? LabNotes { get; set; }

        [MaxLength(100)]
        public string? SourceDeviceId { get; set; }

        [MaxLength(100)]
        public string? SourceTestCode { get; set; }

        [MaxLength(150)]
        public string? PatientIdentifier { get; set; }

        [MaxLength(100)]
        public string? ConnectorSource { get; set; }

        public DateTime? ImportedAt { get; set; }

        public DateTime? SourceTimestamp { get; set; }
    }
}
