using Microsoft.EntityFrameworkCore;
using NagmClinic.Data;
using NagmClinic.Models.Enums;

namespace NagmClinic.Services.Laboratory
{
    public interface ILabDeviceMappingSeedService
    {
        Task EnsureMappingsAsync(CancellationToken cancellationToken = default);
    }

    public class LabDeviceMappingSeedService : ILabDeviceMappingSeedService
    {
        private readonly ApplicationDbContext _context;

        public LabDeviceMappingSeedService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task EnsureMappingsAsync(CancellationToken cancellationToken = default)
        {
            var tests = await _context.ClinicServices
                .Include(s => s.LabAnalyzer)
                .Where(s =>
                    s.Type == ServiceType.LabTest &&
                    s.IsDeviceMapped &&
                    s.SourceType != LabTestSourceType.Manual &&
                    s.DeviceCode != null &&
                    s.LabAnalyzer != null &&
                    s.LabAnalyzer.Code != null)
                .ToListAsync(cancellationToken);

            foreach (var test in tests)
            {
                var deviceId = test.LabAnalyzer!.Code!.Trim().ToUpperInvariant();
                var deviceCode = test.DeviceCode!.Trim().ToUpperInvariant();

                var exists = await _context.LabDeviceTestMappings
                    .AnyAsync(m => m.DeviceId == deviceId && m.DeviceTestCode == deviceCode, cancellationToken);
                if (exists)
                {
                    continue;
                }

                _context.LabDeviceTestMappings.Add(new Models.LabDeviceTestMapping
                {
                    DeviceId = deviceId,
                    DeviceTestCode = deviceCode,
                    LabTestId = test.Id,
                    IsActive = true
                });
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
