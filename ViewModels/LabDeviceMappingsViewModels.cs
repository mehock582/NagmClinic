using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace NagmClinic.ViewModels
{
    public class LabDeviceMappingInputViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "يرجى اختيار الجهاز")]
        [MaxLength(100)]
        public string DeviceId { get; set; } = string.Empty;

        [Required(ErrorMessage = "يرجى إدخال كود الفحص على الجهاز")]
        [MaxLength(100)]
        public string DeviceTestCode { get; set; } = string.Empty;

        [Required(ErrorMessage = "يرجى اختيار الفحص المعملي")]
        public int LabTestId { get; set; }

        public bool IsActive { get; set; } = true;
    }

    public class LabDeviceMappingsIndexViewModel
    {
        public IEnumerable<SelectListItem> DeviceOptions { get; set; } = new List<SelectListItem>();
        public IEnumerable<SelectListItem> LabTests { get; set; } = new List<SelectListItem>();
    }
}
