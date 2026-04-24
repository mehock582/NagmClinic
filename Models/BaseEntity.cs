using System;
using NagmClinic.Interfaces;

namespace NagmClinic.Models
{
    public abstract class BaseEntity : IAuditableEntity
    {
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
