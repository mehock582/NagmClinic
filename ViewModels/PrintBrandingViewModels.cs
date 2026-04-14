using System;

namespace NagmClinic.ViewModels
{
    public class PrintBrandingHeaderViewModel
    {
        public string DocumentTitleAr { get; set; } = string.Empty;
        public string? DocumentTitleEn { get; set; }
        public string? DocumentNumber { get; set; }
        public bool Compact { get; set; }
        public bool ReverseOrder { get; set; }
    }

    public class PrintBrandingFooterViewModel
    {
        public DateTime? PrintedAt { get; set; }
        public string? LeftTitle { get; set; }
        public string? LeftValue { get; set; }
        public string? SecondaryLine { get; set; }
        public bool Compact { get; set; }
        public bool SwapSides { get; set; }
    }
}
