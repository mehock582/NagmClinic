namespace NagmClinic.Extensions
{
    public static class LabDisplayExtensions
    {
        public static string BuildOperationalTestDisplay(string? code, string? englishName, string? fallbackName)
        {
            var normalizedCode = Normalize(code);
            var normalizedEnglishName = Normalize(englishName);
            var normalizedFallbackName = Normalize(fallbackName);

            if (!string.IsNullOrWhiteSpace(normalizedCode) && !string.IsNullOrWhiteSpace(normalizedEnglishName))
            {
                return $"{normalizedCode} - {normalizedEnglishName}";
            }

            if (!string.IsNullOrWhiteSpace(normalizedCode) && !string.IsNullOrWhiteSpace(normalizedFallbackName))
            {
                return $"{normalizedCode} - {normalizedFallbackName}";
            }

            return normalizedEnglishName ?? normalizedFallbackName ?? normalizedCode ?? "-";
        }

        private static string? Normalize(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }
    }
}
