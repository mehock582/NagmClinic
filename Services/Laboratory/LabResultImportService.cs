using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NagmClinic.Data;
using NagmClinic.Models;
using NagmClinic.Models.Enums;

namespace NagmClinic.Services.Laboratory
{
    public interface ILabResultImportService
    {
        Task<LabResultsImportResponse> ImportAsync(LabResultsImportRequest request, CancellationToken cancellationToken = default);
    }

    public class LabResultImportService : ILabResultImportService
    {
        private readonly ApplicationDbContext _context;
        private readonly ILabPatientMatchResolver _patientMatchResolver;
        private readonly ILabDeviceTestMappingService _mappingService;
        private readonly ILogger<LabResultImportService> _logger;

        public LabResultImportService(
            ApplicationDbContext context,
            ILabPatientMatchResolver patientMatchResolver,
            ILabDeviceTestMappingService mappingService,
            ILogger<LabResultImportService> logger)
        {
            _context = context;
            _patientMatchResolver = patientMatchResolver;
            _mappingService = mappingService;
            _logger = logger;
        }

        public async Task<LabResultsImportResponse> ImportAsync(LabResultsImportRequest request, CancellationToken cancellationToken = default)
        {
            var response = new LabResultsImportResponse
            {
                Total = request.Results.Count
            };

            var connectorSource = Normalize(request.ConnectorSource) ?? "CONNECTOR";
            foreach (var item in request.Results)
            {
                var outcome = await ProcessItemAsync(item, connectorSource, cancellationToken);
                response.Items.Add(outcome);

                switch (outcome.Status)
                {
                    case "Imported":
                        response.Imported++;
                        break;
                    case "Duplicate":
                        response.Duplicates++;
                        break;
                    default:
                        response.Rejected++;
                        break;
                }
            }

            return response;
        }

        private async Task<LabImportItemOutcome> ProcessItemAsync(
            NormalizedLabResultItem item,
            string connectorSource,
            CancellationToken cancellationToken)
        {
            var normalizedDeviceId = Normalize(item.DeviceId);
            var normalizedPatientIdentifier = Normalize(item.PatientIdentifier);
            var normalizedTestCode = Normalize(item.TestCode);
            var normalizedResultValue = item.ResultValue?.Trim();
            var normalizedUnit = Normalize(item.Unit);
            var timestamp = item.Timestamp == default ? DateTime.UtcNow : item.Timestamp;

            var outcome = new LabImportItemOutcome
            {
                DeviceId = normalizedDeviceId ?? item.DeviceId,
                PatientIdentifier = normalizedPatientIdentifier ?? item.PatientIdentifier,
                TestCode = normalizedTestCode ?? item.TestCode,
                Timestamp = timestamp
            };

            if (normalizedDeviceId == null ||
                normalizedPatientIdentifier == null ||
                normalizedTestCode == null ||
                string.IsNullOrWhiteSpace(normalizedResultValue))
            {
                outcome.Status = "Rejected";
                outcome.Message = "Missing required fields.";
                return outcome;
            }

            var existingRecord = await _context.LabResultImportRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(r =>
                    r.DeviceId == normalizedDeviceId &&
                    r.TestCode == normalizedTestCode &&
                    r.PatientIdentifier == normalizedPatientIdentifier &&
                    r.Timestamp == timestamp, cancellationToken);
            if (existingRecord != null)
            {
                _logger.LogWarning(
                    "Duplicate lab import skipped: {DeviceId}/{TestCode}/{PatientIdentifier}/{Timestamp}",
                    normalizedDeviceId,
                    normalizedTestCode,
                    normalizedPatientIdentifier,
                    timestamp);

                outcome.Status = "Duplicate";
                outcome.Message = "Duplicate payload ignored.";
                return outcome;
            }

            var importRecord = new LabResultImportRecord
            {
                DeviceId = normalizedDeviceId,
                TestCode = normalizedTestCode,
                PatientIdentifier = normalizedPatientIdentifier,
                ResultValue = normalizedResultValue!,
                Unit = normalizedUnit,
                Timestamp = timestamp,
                ConnectorSource = connectorSource,
                ProcessingStatus = LabImportProcessingStatus.Pending,
                RawPayload = item.RawPayload
            };

            try
            {
                _context.LabResultImportRecords.Add(importRecord);
                await _context.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
            {
                outcome.Status = "Duplicate";
                outcome.Message = "Duplicate payload ignored.";
                return outcome;
            }

            try
            {
                _logger.LogInformation(
                    "Lab import received | Device: {DeviceId} | Test: {TestCode} | Identifier: {PatientIdentifier}",
                    normalizedDeviceId,
                    normalizedTestCode,
                    normalizedPatientIdentifier);

                var appointment = await _patientMatchResolver.ResolveAppointmentAsync(normalizedPatientIdentifier, cancellationToken);
                if (appointment == null)
                {
                    return await RejectAsync(importRecord, "Patient identifier not matched to appointment.", outcome, cancellationToken);
                }

                var mappedTest = await _mappingService.ResolveLabTestAsync(normalizedDeviceId, normalizedTestCode, cancellationToken);
                if (mappedTest == null)
                {
                    // Fallback: if mapping is missing, try to match by requested test code in the same appointment.
                    // This keeps import resilient for clinics that have not configured device mappings for every test yet.
                    var codeMatches = appointment.AppointmentItems
                        .Where(ai =>
                            ai.Service != null &&
                            ai.Service.Type == ServiceType.LabTest &&
                            ai.Service.IsActive &&
                            string.Equals(ai.Service.Code, normalizedTestCode, StringComparison.OrdinalIgnoreCase))
                        .Select(ai => ai.Service!)
                        .DistinctBy(s => s.Id)
                        .ToList();

                    if (codeMatches.Count == 1)
                    {
                        mappedTest = codeMatches[0];
                        _logger.LogWarning(
                            "Lab import fallback mapping by requested test code used | Device={DeviceId} Test={TestCode} AppointmentId={AppointmentId}",
                            normalizedDeviceId,
                            normalizedTestCode,
                            appointment.Id);
                    }
                }

                if (mappedTest == null)
                {
                    return await RejectAsync(importRecord, "No Device->LabTest mapping found.", outcome, cancellationToken);
                }

                var appointmentItem = appointment.AppointmentItems
                    .Where(ai => ai.ServiceId == mappedTest.Id)
                    .OrderBy(ai => ai.LabResult != null && ai.LabResult.Status == LabStatus.Pending ? 0 : 1)
                    .ThenBy(ai => ai.Id)
                    .FirstOrDefault();
                if (appointmentItem == null)
                {
                    appointmentItem = new AppointmentItem
                    {
                        AppointmentId = appointment.Id,
                        ServiceId = mappedTest.Id,
                        Quantity = 1,
                        UnitPrice = mappedTest.Price,
                        TotalPrice = mappedTest.Price,
                        Status = PaymentStatus.Pending
                    };
                    _context.AppointmentItems.Add(appointmentItem);
                    // We must add it to the navigation property if it was eagerly loaded
                    if (appointment.AppointmentItems is List<AppointmentItem> list)
                    {
                        list.Add(appointmentItem);
                    }
                }

                var labResult = appointmentItem.LabResult;
                if (labResult == null)
                {
                    labResult = new LabResult
                    {
                        AppointmentItemId = appointmentItem.Id,
                        RequestedAt = DateTime.Now,
                        Status = LabStatus.Pending
                    };
                    _context.LabResults.Add(labResult);
                }

                labResult.ResultValue = normalizedResultValue;
                labResult.Unit = normalizedUnit ?? mappedTest.Unit;
                labResult.NormalRange = !string.IsNullOrWhiteSpace(mappedTest.ReferenceRange)
                    ? mappedTest.ReferenceRange
                    : (mappedTest.NormalRange ?? labResult.NormalRange);
                labResult.Status = LabStatus.Completed;
                labResult.PerformedBy = normalizedDeviceId;
                labResult.PerformedAt = timestamp;
                labResult.SourceDeviceId = normalizedDeviceId;
                labResult.SourceTestCode = normalizedTestCode;
                labResult.PatientIdentifier = normalizedPatientIdentifier;
                labResult.ConnectorSource = connectorSource;
                labResult.ImportedAt = DateTime.UtcNow;
                labResult.SourceTimestamp = timestamp;
                labResult.LabNotes = $"Auto-imported from {normalizedDeviceId} via {connectorSource}.";

                importRecord.LabResult = labResult;
                importRecord.ProcessingStatus = LabImportProcessingStatus.Imported;
                importRecord.ErrorMessage = null;

                await _context.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Lab import mapped successfully | AppointmentId: {AppointmentId} | LabResultId: {LabResultId}",
                    appointment.Id,
                    labResult.Id);

                outcome.Status = "Imported";
                outcome.Message = "Imported successfully.";
                return outcome;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lab import failed for DeviceId={DeviceId}, TestCode={TestCode}", normalizedDeviceId, normalizedTestCode);

                importRecord.ProcessingStatus = LabImportProcessingStatus.Failed;
                importRecord.ErrorMessage = ex.Message;
                await _context.SaveChangesAsync(cancellationToken);

                outcome.Status = "Rejected";
                outcome.Message = "Import failed.";
                return outcome;
            }
        }

        private async Task<LabImportItemOutcome> RejectAsync(
            LabResultImportRecord importRecord,
            string errorMessage,
            LabImportItemOutcome outcome,
            CancellationToken cancellationToken)
        {
            importRecord.ProcessingStatus = LabImportProcessingStatus.Rejected;
            importRecord.ErrorMessage = errorMessage;
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogWarning(
                "Lab import rejected | Device: {DeviceId} | Test: {TestCode} | Identifier: {PatientIdentifier} | Reason: {Reason}",
                importRecord.DeviceId,
                importRecord.TestCode,
                importRecord.PatientIdentifier,
                errorMessage);

            outcome.Status = "Rejected";
            outcome.Message = errorMessage;
            return outcome;
        }

        private static bool IsUniqueConstraintViolation(DbUpdateException ex)
        {
            return ex.InnerException is SqlException sqlEx &&
                   (sqlEx.Number == 2601 || sqlEx.Number == 2627);
        }

        private static string? Normalize(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
        }
    }
}
