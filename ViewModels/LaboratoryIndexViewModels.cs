using System;
using System.Collections.Generic;

namespace NagmClinic.ViewModels
{
    /// <summary>
    /// One row per patient/appointment group on the Laboratory Index page.
    /// </summary>
    public class LabIndexPatientRowDto
    {
        public int AppointmentId { get; set; }
        public long DailyNumber { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string PatientPhone { get; set; } = "-";
        public string DoctorName { get; set; } = string.Empty;
        public string RequestDate { get; set; } = string.Empty;
        public int TotalTests { get; set; }
        public int CompletedTests { get; set; }
        public int PendingTests { get; set; }
        public string OverallStatus { get; set; } = string.Empty;
    }

    /// <summary>
    /// Individual lab test detail for the expanded/detail view.
    /// </summary>
    public class LabIndexTestDetailDto
    {
        public int LabResultId { get; set; }
        public string TestName { get; set; } = string.Empty;
        public string TestNameAr { get; set; } = string.Empty;
        public string? TestCode { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? ResultValue { get; set; }
        public string? Unit { get; set; }
        public string? NormalRange { get; set; }
        public string? LabNotes { get; set; }
        public string? PerformedBy { get; set; }
        public int ResultType { get; set; }
        public string? PredefinedValues { get; set; }
        public int SourceType { get; set; }
        public string? CategoryName { get; set; }
    }
}
