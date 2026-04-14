namespace NagmClinic.Models.Enums
{
    public enum ServiceType
    {
        Service = 1,
        LabTest = 2
    }

    public enum AppointmentStatus
    {
        Confirmed = 2,
        Cancelled = 3
    }

    public enum LabStatus
    {
        Pending = 1,
        InProgress = 2,
        Completed = 3,
        Cancelled = 4
    }

    public enum Gender
    {
        Male = 1,
        Female = 2
    }

    public enum LabResultType
    {
        Text = 1,
        Number = 2,
        Dropdown = 3,
        PositiveNegative = 4,
        Calculated = 5
    }

    public enum LabTestSourceType
    {
        Manual = 1,
        Device = 2,
        Hybrid = 3
    }

    public enum PharmacySaleStatus
    {
        Completed = 1,
        Voided = 2
    }

    public enum LabImportProcessingStatus
    {
        Pending = 1,
        Imported = 2,
        Rejected = 3,
        Duplicate = 4,
        Failed = 5
    }
}
