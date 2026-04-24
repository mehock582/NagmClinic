using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using NagmClinic.Models.Enums;

namespace NagmClinic.Models
{
    public class ClinicService : BaseEntity
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم الخدمة مطلوب")]
        [Display(Name = "اسم الخدمة (عربي)")]
        public string NameAr { get; set; } = string.Empty;

        [Required(ErrorMessage = "English Name Required")]
        [Display(Name = "اسم الخدمة (إنجليزي)")]
        public string NameEn { get; set; } = string.Empty;

        [Display(Name = "كود الفحص")]
        [MaxLength(50)]
        public string? Code { get; set; }

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

        [Display(Name = "المدى المرجعي")]
        public string? ReferenceRange { get; set; }

        [Display(Name = "المدى الحرج")]
        public string? CriticalRange { get; set; }

        [Display(Name = "اسم الطباعة")]
        [MaxLength(200)]
        public string? PrintName { get; set; }

        [Display(Name = "نوع العينة")]
        [MaxLength(100)]
        public string? SampleType { get; set; }

        [Display(Name = "نوع النتيجة")]
        public LabResultType ResultType { get; set; } = LabResultType.Text;

        [Display(Name = "مصدر النتيجة")]
        public LabTestSourceType SourceType { get; set; } = LabTestSourceType.Manual;

        [Display(Name = "مربوط بجهاز")]
        public bool IsDeviceMapped { get; set; }

        [Display(Name = "كود الجهاز")]
        [MaxLength(100)]
        public string? DeviceCode { get; set; }

        [Display(Name = "القيم المحددة مسبقاً")]
        public string? PredefinedValues { get; set; }

        [Display(Name = "ترتيب العرض")]
        public int SortOrder { get; set; }

        [Display(Name = "التصنيف")]
        public int? LabCategoryId { get; set; }
        public virtual LabCategory? LabCategory { get; set; }

        [Display(Name = "الجهاز")]
        public int? LabAnalyzerId { get; set; }
        public virtual LabAnalyzer? LabAnalyzer { get; set; }

        public bool IsActive { get; set; } = true;

        public virtual ICollection<AppointmentItem> AppointmentItems { get; set; } = new List<AppointmentItem>();
    }
}
