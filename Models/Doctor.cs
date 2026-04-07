using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NagmClinic.Models
{
    public class Doctor
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "اسم الطبيب مطلوب")]
        [Display(Name = "اسم الطبيب")]
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

        [Required(ErrorMessage = "سعر الكشف مطلوب")]
        [Display(Name = "سعر الكشف")]
        public decimal ConsultationFee { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual ICollection<Appointment> Appointments { get; set; } = new List<Appointment>();
    }
}
