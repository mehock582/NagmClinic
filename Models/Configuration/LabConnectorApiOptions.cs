namespace NagmClinic.Models.Configuration
{
    public class LabConnectorApiOptions
    {
        public const string SectionName = "LabConnectorApi";

        public string ApiKeyHeaderName { get; set; } = "X-Connector-Api-Key";

        public string ApiKey { get; set; } = string.Empty;

        public bool AllowHttpInDevelopment { get; set; }

        public bool AllowAnonymousInDevelopment { get; set; }
    }
}
