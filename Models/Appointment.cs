using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using NagmClinic.Models.Enums;

namespace NagmClinic.Models
{
    public class Appointment
    {
        public int Id { get; set; }

        [Display(Name = "تاريخ الموعد")]
        public DateTime AppointmentDate { get; set; } = DateTime.Now;

        [Display(Name = "رقم الحجز اليومي")]
        public long DailyNumber { get; set; }

        [Required(ErrorMessage = "يجب اختيار المريض")]
        public int PatientId { get; set; }
        public virtual Patient? Patient { get; set; }

        [Required(ErrorMessage = "يجب اختيار الطبيب")]
        public int DoctorId { get; set; }
        public virtual Doctor? Doctor { get; set; }

        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        [Display(Name = "سعر الكشف")]
        public decimal ConsultationFee { get; set; }

        [Display(Name = "سبب الإعفاء من الرسوم")]
        public string? ZeroFeeReason { get; set; }

        [Display(Name = "الحالة")]
        public AppointmentStatus Status { get; set; } = AppointmentStatus.Confirmed;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual ICollection<AppointmentItem> AppointmentItems { get; set; } = new List<AppointmentItem>();
    }
}
