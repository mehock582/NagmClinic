using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using NagmClinic.Models.Enums;

namespace NagmClinic.ViewModels
{
    public class AppointmentCreateViewModel
    {
        public int Id { get; set; }

        [Display(Name = "المريض")]
        [Range(1, int.MaxValue, ErrorMessage = "اختيار المريض مطلوب")]
        public int PatientId { get; set; }

        [Required(ErrorMessage = "يجب اختيار الطبيب")]
        public int DoctorId { get; set; }

        [Required(ErrorMessage = "يجب تحديد تاريخ الموعد")]
        public DateTime AppointmentDate { get; set; } = DateTime.Now;

        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        [Display(Name = "سعر الكشف")]
        public decimal ConsultationFee { get; set; }

        [Display(Name = "سبب الإعفاء من الرسوم")]
        public string? ZeroFeeReason { get; set; }

        public bool PrintReceipt { get; set; }

        // Display-only properties for Edit page (eliminates ViewBag)
        public string? PatientName { get; set; }
        public string? DoctorName { get; set; }
        public int? DailyNumber { get; set; }

        public List<ServiceItemDto> ServicesData { get; set; } = new List<ServiceItemDto>();
        public IEnumerable<SelectListItem> AvailableDoctors { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> AvailableServices { get; set; } = new List<SelectListItem>();
        public Dictionary<int, decimal> DoctorFees { get; set; } = new Dictionary<int, decimal>();

        public List<AppointmentItemViewModel> Items { get; set; } = new List<AppointmentItemViewModel>();
    }

    public class AppointmentItemViewModel
    {
        public int AppointmentItemId { get; set; }
        public byte[]? RowVersion { get; set; }
        public int ServiceId { get; set; }
        public string ServiceName { get; set; } = string.Empty;
        public string ServiceDisplayName { get; set; } = string.Empty;
        public int Quantity { get; set; } = 1;
        public decimal UnitPrice { get; set; }
        public bool HasRecordedLabResult { get; set; }
        public NagmClinic.Models.Enums.ServiceType ItemType { get; set; }
    }

    public class ServiceItemDto
    {
        public int Id { get; set; }
        public string NameAr { get; set; } = string.Empty;
        public string NameEn { get; set; } = string.Empty;
        public string? Code { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public NagmClinic.Models.Enums.ServiceType Type { get; set; }
    }

    public class PatientViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "الاسم بالكامل مطلوب")]
        [Display(Name = "الاسم بالكامل")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "رقم الهاتف مطلوب")]
        [Display(Name = "رقم الهاتف")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Required(ErrorMessage = "العمر مطلوب")]
        [Display(Name = "العمر")]
        public int Age { get; set; }

        [Display(Name = "النوع")]
        public Gender Gender { get; set; }

        [Display(Name = "العنوان")]
        public string? Address { get; set; }
    }

    public class DoctorViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم الطبيب مطلوب")]
        [Display(Name = "اسم الطبيب (عربي)")]
        public string NameAr { get; set; } = string.Empty;

        [Display(Name = "الاسم بالإنجليزية")]
        public string NameEn { get; set; } = string.Empty;

        [Required(ErrorMessage = "التخصص مطلوب")]
        [Display(Name = "التخصص")]
        public string Specialty { get; set; } = string.Empty;

        [Display(Name = "رقم الهاتف")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Display(Name = "رقم الغرفة")]
        public string RoomNumber { get; set; } = string.Empty;

        [Display(Name = "سعر الكشف")]
        public decimal ConsultationFee { get; set; }
        
        [Display(Name = "نشيط؟")]
        public bool IsActive { get; set; } = true;
    }

    public class ClinicServiceViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم الخدمة مطلوب")]
        [Display(Name = "اسم الخدمة (عربي)")]
        public string NameAr { get; set; } = string.Empty;

        [Display(Name = "اسم الخدمة (إنجليزي)")]
        public string NameEn { get; set; } = string.Empty;

        [Display(Name = "كود الفحص")]
        public string? Code { get; set; }

        [Required(ErrorMessage = "نوع الخدمة مطلوب")]
        [Display(Name = "نوع الخدمة")]
        public ServiceType Type { get; set; }

        [Required(ErrorMessage = "السعر مطلوب")]
        [Display(Name = "السعر")]
        public decimal Price { get; set; }

        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        [Display(Name = "الوحدة (اختياري)")]
        public string? Unit { get; set; }

        [Display(Name = "المدى الطبيعي (اختياري)")]
        public string? NormalRange { get; set; }

        [Display(Name = "المدى المرجعي (اختياري)")]
        public string? ReferenceRange { get; set; }

        [Display(Name = "المدى الحرج (اختياري)")]
        public string? CriticalRange { get; set; }

        [Display(Name = "اسم الطباعة")]
        public string? PrintName { get; set; }

        [Display(Name = "نوع العينة")]
        public string? SampleType { get; set; }

        [Display(Name = "نوع النتيجة")]
        public LabResultType ResultType { get; set; }

        [Display(Name = "مصدر النتيجة")]
        public LabTestSourceType SourceType { get; set; } = LabTestSourceType.Manual;

        [Display(Name = "مربوط بجهاز")]
        public bool IsDeviceMapped { get; set; }

        [Display(Name = "كود الجهاز")]
        public string? DeviceCode { get; set; }

        [Display(Name = "القيم المحددة مسبقاً (فاصلة)")]
        public string? PredefinedValues { get; set; }

        [Display(Name = "ترتيب العرض")]
        public int SortOrder { get; set; }

        [Display(Name = "التصنيف")]
        public int? LabCategoryId { get; set; }

        [Display(Name = "الجهاز")]
        public int? LabAnalyzerId { get; set; }

        [Display(Name = "نشيط؟")]
        public bool IsActive { get; set; } = true;
    }

    public class LabTestsIndexViewModel
    {
        public IEnumerable<SelectListItem> Categories { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> Analyzers { get; set; } = new List<SelectListItem>();
    }
}
