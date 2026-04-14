using System;
using System.Collections.Generic;

namespace NagmClinic.ViewModels
{
    public class LabReportPrintViewModel
    {
        public LabReportHeaderViewModel Header { get; set; } = new();
        public LabReportPatientInfoViewModel Patient { get; set; } = new();
        public LabReportMetaViewModel Meta { get; set; } = new();
        public LabReportFooterViewModel Footer { get; set; } = new();
        public List<LabReportResultGroupViewModel> ResultGroups { get; set; } = new();
    }

    public class LabReportHeaderViewModel
    {
        public string CenterNameAr { get; set; } = string.Empty;
        public string? CenterNameEn { get; set; }
        public string? LogoUrl { get; set; }
    }

    public class LabReportPatientInfoViewModel
    {
        public int PatientId { get; set; }
        public string PatientName { get; set; } = string.Empty;
        public string GenderAr { get; set; } = "-";
        public string GenderEn { get; set; } = "-";
        public int Age { get; set; }
        public string Phone { get; set; } = "-";
        public string AttendingPhysician { get; set; } = "-";
        public string? QrPayload { get; set; }
        public string? QrCodeDataUri { get; set; }
    }

    public class LabReportMetaViewModel
    {
        public string LabNumber { get; set; } = string.Empty;
        public long DailyNumber { get; set; }
        public DateTime AppointmentDate { get; set; }
        public DateTime ReportDate { get; set; }
        public DateTime PrintedAt { get; set; }
    }

    public class LabReportFooterViewModel
    {
        public string AddressAr { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string? PrintedBy { get; set; }
    }

    public class LabReportResultGroupViewModel
    {
        public string GroupNameAr { get; set; } = string.Empty;
        public string GroupNameEn { get; set; } = string.Empty;
        public List<LabReportResultRowViewModel> Rows { get; set; } = new();
    }

    public class LabReportResultRowViewModel
    {
        public string TestNameAr { get; set; } = string.Empty;
        public string? TestNameEn { get; set; }
        public string Reading { get; set; } = "-";
        public string Flag { get; set; } = string.Empty;
        public string Unit { get; set; } = "-";
        public string ReferenceRange { get; set; } = "-";
    }
}
