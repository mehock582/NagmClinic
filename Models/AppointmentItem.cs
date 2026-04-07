using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NagmClinic.Models
{
    public class AppointmentItem
    {
        public int Id { get; set; }

        public int AppointmentId { get; set; }
        public virtual Appointment Appointment { get; set; } = default!;

        public int ServiceId { get; set; }
        public virtual ClinicService Service { get; set; } = default!;

        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalPrice { get; set; }

        public virtual LabResult? LabResult { get; set; }
    }
}
