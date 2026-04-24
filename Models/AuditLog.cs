using System;
using System.ComponentModel.DataAnnotations;

namespace NagmClinic.Models
{
    public class AuditLog
    {
        [Key]
        public int Id { get; set; }

        public string? UserId { get; set; }

        [Required]
        public string TableName { get; set; } = string.Empty;

        [Required]
        public string Action { get; set; } = string.Empty; // Insert, Update, Delete

        public string? OldValues { get; set; } // JSON

        public string? NewValues { get; set; } // JSON

        [Required]
        public DateTime Timestamp { get; set; } = DateTime.Now;
    }
}
