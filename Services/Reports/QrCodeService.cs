using QRCoder;

namespace NagmClinic.Services.Reports
{
    public class QrCodeService : IQrCodeService
    {
        public string GeneratePngDataUri(string payload, int pixelsPerModule = 5)
        {
            var text = string.IsNullOrWhiteSpace(payload) ? "-" : payload;

            using var qrGenerator = new QRCodeGenerator();
            using var qrData = qrGenerator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
            var png = new PngByteQRCode(qrData);
            var bytes = png.GetGraphic(pixelsPerModule, drawQuietZones: true);
            return "data:image/png;base64," + Convert.ToBase64String(bytes);
        }
    }
}
