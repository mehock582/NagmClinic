using System.Globalization;
using Microsoft.EntityFrameworkCore;
using NagmClinic.Data;
using NagmClinic.Models;

namespace NagmClinic.Services.Laboratory
{
    public interface ILabPatientMatchResolver
    {
        Task<Appointment?> ResolveAppointmentAsync(string patientIdentifier, CancellationToken cancellationToken = default);
    }

    public class LabPatientMatchResolver : ILabPatientMatchResolver
    {
        private static readonly string[] AppointmentIdPrefixes = { "APPT:", "APPOINTMENT:", "REQ:", "REQUEST:" };
        private static readonly string[] DailyNumberPrefixes = { "VISIT:", "DAILY:", "LABNO:", "LAB:" };
        private readonly ApplicationDbContext _context;

        public LabPatientMatchResolver(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Appointment?> ResolveAppointmentAsync(string patientIdentifier, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(patientIdentifier))
            {
                return null;
            }

            var normalized = patientIdentifier.Trim();
            if (TryParseDailyNumberFromLabNumber(normalized, out var labDailyNumber))
            {
                return await LoadAppointmentByDailyNumberAsync(labDailyNumber, cancellationToken);
            }

            if (TryExtractNumberByPrefixes(normalized, AppointmentIdPrefixes, out var appointmentId))
            {
                return await LoadAppointmentByIdAsync((int)appointmentId, cancellationToken);
            }

            if (TryExtractNumberByPrefixes(normalized, DailyNumberPrefixes, out var dailyNumber))
            {
                return await LoadAppointmentByDailyNumberAsync(dailyNumber, cancellationToken);
            }

            if (long.TryParse(normalized, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericIdentifier))
            {
                var byDailyNumber = await LoadAppointmentByDailyNumberAsync(numericIdentifier, cancellationToken);
                if (byDailyNumber != null)
                {
                    return byDailyNumber;
                }

                if (numericIdentifier > 0 && numericIdentifier <= int.MaxValue)
                {
                    return await LoadAppointmentByIdAsync((int)numericIdentifier, cancellationToken);
                }
            }

            return null;
        }

        private async Task<Appointment?> LoadAppointmentByIdAsync(int appointmentId, CancellationToken cancellationToken)
        {
            return await _context.Appointments
                .Include(a => a.AppointmentItems)
                    .ThenInclude(ai => ai.Service)
                .Include(a => a.AppointmentItems)
                    .ThenInclude(ai => ai.LabResult)
                .FirstOrDefaultAsync(a => a.Id == appointmentId, cancellationToken);
        }

        private async Task<Appointment?> LoadAppointmentByDailyNumberAsync(long dailyNumber, CancellationToken cancellationToken)
        {
            return await _context.Appointments
                .Include(a => a.AppointmentItems)
                    .ThenInclude(ai => ai.Service)
                .Include(a => a.AppointmentItems)
                    .ThenInclude(ai => ai.LabResult)
                .OrderByDescending(a => a.AppointmentDate)
                .FirstOrDefaultAsync(a => a.DailyNumber == dailyNumber, cancellationToken);
        }

        private static bool TryExtractNumberByPrefixes(string value, IEnumerable<string> prefixes, out long number)
        {
            foreach (var prefix in prefixes)
            {
                if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var rawNumber = value[prefix.Length..].Trim();
                if (long.TryParse(rawNumber, NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
                {
                    return true;
                }
            }

            number = 0;
            return false;
        }

        private static bool TryParseDailyNumberFromLabNumber(string value, out long dailyNumber)
        {
            dailyNumber = 0;
            if (!value.StartsWith("LAB-", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var parts = value.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (parts.Length < 3)
            {
                return false;
            }

            return long.TryParse(parts[^1], NumberStyles.Integer, CultureInfo.InvariantCulture, out dailyNumber);
        }
    }
}
