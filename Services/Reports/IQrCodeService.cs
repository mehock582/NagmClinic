namespace NagmClinic.Services.Reports
{
    public interface IQrCodeService
    {
        string GeneratePngDataUri(string payload, int pixelsPerModule = 5);
    }
}
