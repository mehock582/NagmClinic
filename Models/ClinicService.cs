using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using NagmClinic.Models.Enums;

namespace NagmClinic.Models
{
    public class ClinicService
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم الخدمة مطلوب")]
        [Display(Name = "اسم الخدمة (عربي)")]
        public string NameAr { get; set; } = string.Empty;

        [Required(ErrorMessage = "English Name Required")]
        [Display(Name = "اسم الخدمة (إنجليزي)")]
        public string NameEn { get; set; } = string.Empty;

        [Required(ErrorMessage = "نوع الخدمة مطلوب")]
        [Display(Name = "نوع الخدمة")]
        public ServiceType Type { get; set; }

        [Required(ErrorMessage = "السعر مطلوب")]
        [Display(Name = "السعر")]
        public decimal Price { get; set; }

        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        [Display(Name = "الوحدة")]
        public string? Unit { get; set; }

        [Display(Name = "المدى الطبيعي")]
        public string? NormalRange { get; set; }

        [Display(Name = "نوع النتيجة")]
        public LabResultType ResultType { get; set; } = LabResultType.Text;

        [Display(Name = "القيم المحددة مسبقاً")]
        public string? PredefinedValues { get; set; }

        public bool IsActive { get; set; } = true;

        public virtual ICollection<AppointmentItem> AppointmentItems { get; set; } = new List<AppointmentItem>();
    }
}
