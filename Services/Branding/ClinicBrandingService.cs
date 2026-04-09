using Microsoft.Extensions.Options;
using NagmClinic.Models.Configuration;

namespace NagmClinic.Services.Branding
{
    public class ClinicBrandingService : IClinicBrandingService
    {
        private readonly IOptionsMonitor<ClinicBrandingOptions> _options;
        private readonly IWebHostEnvironment _environment;

        public ClinicBrandingService(IOptionsMonitor<ClinicBrandingOptions> options, IWebHostEnvironment environment)
        {
            _options = options;
            _environment = environment;
        }

        public ClinicBrandingInfo GetBranding()
        {
            var settings = _options.CurrentValue;
            var logoPath = string.IsNullOrWhiteSpace(settings.LogoPath)
                ? "/images/branding/clinic-logo.png"
                : settings.LogoPath.Trim();

            var resolvedLogo = ResolveLogoIfExists(logoPath);
            var fallbackLogo = ResolveLogoIfExists("/images/branding/clinic-logo.png");

            return new ClinicBrandingInfo
            {
                CenterNameAr = settings.CenterNameAr,
                CenterNameEn = settings.CenterNameEn,
                LogoUrl = resolvedLogo ?? fallbackLogo,
                AddressAr = settings.AddressAr,
                Phone = settings.Phone
            };
        }

        private string? ResolveLogoIfExists(string webPath)
        {
            if (string.IsNullOrWhiteSpace(webPath))
            {
                return null;
            }

            var normalized = webPath.StartsWith('/') ? webPath[1..] : webPath;
            var physicalPath = Path.Combine(_environment.WebRootPath ?? string.Empty, normalized.Replace('/', Path.DirectorySeparatorChar));

            if (!string.IsNullOrWhiteSpace(_environment.WebRootPath) && File.Exists(physicalPath))
            {
                return webPath.StartsWith('/') ? webPath : "/" + webPath;
            }

            return null;
        }
    }
}
