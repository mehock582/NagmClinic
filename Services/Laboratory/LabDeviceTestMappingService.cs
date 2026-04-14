using Microsoft.EntityFrameworkCore;
using NagmClinic.Data;
using NagmClinic.Models;
using NagmClinic.Models.Enums;

namespace NagmClinic.Services.Laboratory
{
    public interface ILabDeviceTestMappingService
    {
        Task<ClinicService?> ResolveLabTestAsync(string deviceId, string testCode, CancellationToken cancellationToken = default);
    }

    public class LabDeviceTestMappingService : ILabDeviceTestMappingService
    {
        private readonly ApplicationDbContext _context;

        public LabDeviceTestMappingService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<ClinicService?> ResolveLabTestAsync(string deviceId, string testCode, CancellationToken cancellationToken = default)
        {
            var normalizedDeviceId = Normalize(deviceId);
            var normalizedTestCode = Normalize(testCode);
            if (normalizedDeviceId == null || normalizedTestCode == null)
            {
                return null;
            }

            var mapped = await _context.LabDeviceTestMappings
                .Include(m => m.LabTest)
                .Where(m => m.IsActive &&
                            m.DeviceId == normalizedDeviceId &&
                            m.DeviceTestCode == normalizedTestCode)
                .Select(m => m.LabTest)
                .FirstOrDefaultAsync(cancellationToken);

            if (mapped != null && mapped.Type == ServiceType.LabTest && mapped.IsActive)
            {
                return mapped;
            }

            return await _context.ClinicServices
                .Where(s =>
                    s.Type == ServiceType.LabTest &&
                    s.IsActive &&
                    s.IsDeviceMapped &&
                    s.SourceType != LabTestSourceType.Manual &&
                    s.DeviceCode == normalizedTestCode &&
                    s.LabAnalyzer != null &&
                    s.LabAnalyzer.Code == normalizedDeviceId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        private static string? Normalize(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
        }
    }
}
