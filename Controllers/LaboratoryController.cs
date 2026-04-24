using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NagmClinic.Data;
using NagmClinic.Models;
using NagmClinic.Models.Configuration;
using NagmClinic.Models.Enums;
using NagmClinic.ViewModels;
using NagmClinic.Extensions;
using NagmClinic.Models.DataTables;
using NagmClinic.Services.Branding;
using NagmClinic.Services.Laboratory;
using NagmClinic.Services.Reports;

namespace NagmClinic.Controllers
{
    [Authorize]
    public class LaboratoryController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IClinicBrandingService _brandingService;
        private readonly IQrCodeService _qrCodeService;
        private readonly ILabResultImportService _labResultImportService;
        private readonly LabConnectorApiOptions _labConnectorOptions;
        private readonly IWebHostEnvironment _environment;

        public LaboratoryController(
            ApplicationDbContext context,
            IClinicBrandingService brandingService,
            IQrCodeService qrCodeService,
            ILabResultImportService labResultImportService,
            IOptions<LabConnectorApiOptions> labConnectorOptions,
            IWebHostEnvironment environment)
        {
            _context = context;
            _brandingService = brandingService;
            _qrCodeService = qrCodeService;
            _labResultImportService = labResultImportService;
            _labConnectorOptions = labConnectorOptions.Value;
            _environment = environment;
        }

        // GET: Laboratory
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> GetLabResultsData()
        {
            try
            {
                var dtParams = Request.GetDataTablesParameters();
                var searchValue = dtParams.Search != null && dtParams.Search.ContainsKey("value") ? dtParams.Search["value"] : null;

                // Base query: all lab results with needed navigations
                var query = _context.LabResults
                    .Include(lr => lr.AppointmentItem)
                        .ThenInclude(ai => ai.Appointment)
                            .ThenInclude(a => a.Patient)
                    .Include(lr => lr.AppointmentItem)
                        .ThenInclude(ai => ai.Appointment)
                            .ThenInclude(a => a.Doctor)
                    .Include(lr => lr.AppointmentItem)
                        .ThenInclude(ai => ai.Service)
                    .AsQueryable();

                if (!string.IsNullOrEmpty(searchValue))
                {
                    query = query.Where(lr => (lr.AppointmentItem.Appointment.Patient != null &&
                                               lr.AppointmentItem.Appointment.Patient.FullName.Contains(searchValue)) ||
                                           lr.AppointmentItem.Appointment.DailyNumber.ToString().Contains(searchValue));
                }

                // Group by appointment (= one patient visit)
                var grouped = await query
                    .GroupBy(lr => lr.AppointmentItem.AppointmentId)
                    .Select(g => new
                    {
                        AppointmentId = g.Key,
                        DailyNumber = g.First().AppointmentItem.Appointment.DailyNumber,
                        PatientName = g.First().AppointmentItem.Appointment.Patient != null
                            ? g.First().AppointmentItem.Appointment.Patient!.FullName : "غير معروف",
                        PatientPhone = g.First().AppointmentItem.Appointment.Patient != null
                            ? g.First().AppointmentItem.Appointment.Patient!.PhoneNumber : "-",
                        DoctorName = g.First().AppointmentItem.Appointment.Doctor != null
                            ? g.First().AppointmentItem.Appointment.Doctor!.NameAr : "",
                        RequestDate = g.Max(lr => lr.RequestedAt),
                        TotalTests = g.Count(),
                        CompletedTests = g.Count(lr => lr.Status == LabStatus.Completed || !string.IsNullOrWhiteSpace(lr.ResultValue)),
                        PendingTests = g.Count(lr => lr.Status != LabStatus.Completed && string.IsNullOrWhiteSpace(lr.ResultValue))
                    })
                    .OrderByDescending(g => g.RequestDate)
                    .ToListAsync();

                int recordsTotal = grouped.Count;

                var paged = grouped
                    .Skip(dtParams.Start)
                    .Take(dtParams.Length)
                    .Select(g => new LabIndexPatientRowDto
                    {
                        AppointmentId = g.AppointmentId,
                        DailyNumber = g.DailyNumber,
                        PatientName = g.PatientName,
                        PatientPhone = g.PatientPhone,
                        DoctorName = g.DoctorName,
                        RequestDate = g.RequestDate.ToString("yyyy-MM-dd HH:mm"),
                        TotalTests = g.TotalTests,
                        CompletedTests = g.CompletedTests,
                        PendingTests = g.PendingTests,
                        OverallStatus = g.PendingTests == 0 ? "Completed" : (g.CompletedTests > 0 ? "Partial" : "Pending")
                    })
                    .ToList();

                return Json(new DataTablesResponse<object>
                {
                    draw = dtParams.Draw,
                    recordsTotal = recordsTotal,
                    recordsFiltered = recordsTotal,
                    data = paged
                });
            }
            catch (Exception ex) { return Json(new DataTablesResponse<object> { error = ex.Message }); }
        }

        // AJAX: Get all lab test details for one appointment (used by expand/detail panel)
        [HttpGet]
        public async Task<IActionResult> GetLabTestDetails(int appointmentId)
        {
            var tests = await _context.LabResults
                .Include(lr => lr.AppointmentItem)
                    .ThenInclude(ai => ai.Appointment)
                        .ThenInclude(a => a.Patient)
                .Include(lr => lr.AppointmentItem)
                    .ThenInclude(ai => ai.Service)
                        .ThenInclude(s => s.LabCategory)
                .Where(lr => lr.AppointmentItem.AppointmentId == appointmentId)
                .Select(lr => new LabIndexTestDetailDto
                {
                    LabResultId = lr.Id,
                    TestName = LabDisplayExtensions.BuildOperationalTestDisplay(
                        lr.AppointmentItem.Service != null ? lr.AppointmentItem.Service.Code : null,
                        lr.AppointmentItem.Service != null ? lr.AppointmentItem.Service.NameEn : null,
                        lr.AppointmentItem.Service != null ? lr.AppointmentItem.Service.NameAr : "-"),
                    TestNameAr = lr.AppointmentItem.Service != null ? lr.AppointmentItem.Service.NameAr : "-",
                    TestCode = lr.AppointmentItem.Service != null ? lr.AppointmentItem.Service.Code : null,
                    Status = (!string.IsNullOrWhiteSpace(lr.ResultValue) ? LabStatus.Completed : lr.Status).ToString(),
                    ResultValue = lr.ResultValue,
                    Unit = lr.Unit,
                    NormalRange = FormatReferenceRange(lr.NormalRange, lr.AppointmentItem.Appointment.Patient != null ? lr.AppointmentItem.Appointment.Patient.Gender : (Gender?)null),
                    LabNotes = lr.LabNotes,
                    PerformedBy = lr.PerformedBy,
                    ResultType = lr.AppointmentItem.Service != null ? (int)lr.AppointmentItem.Service.ResultType : 0,
                    PredefinedValues = lr.AppointmentItem.Service != null ? lr.AppointmentItem.Service.PredefinedValues : null,
                    SourceType = lr.AppointmentItem.Service != null ? (int)lr.AppointmentItem.Service.SourceType : 0,
                    CategoryName = (lr.AppointmentItem.Service != null && lr.AppointmentItem.Service.LabCategory != null)
                        ? lr.AppointmentItem.Service.LabCategory.NameAr : null
                })
                .ToListAsync();

            return Json(tests);
        }

        [HttpGet]
        public async Task<IActionResult> GetIntegrationStatus()
        {
            var nowUtc = DateTime.UtcNow;
            const int minutesWindow = 30;
            var thresholdUtc = nowUtc.AddMinutes(-minutesWindow);

            var recentImportedCount = await _context.LabResultImportRecords
                .CountAsync(r =>
                    r.ImportedAt >= thresholdUtc &&
                    r.ProcessingStatus == LabImportProcessingStatus.Imported);

            var recentRejectedCount = await _context.LabResultImportRecords
                .CountAsync(r =>
                    r.ImportedAt >= thresholdUtc &&
                    (r.ProcessingStatus == LabImportProcessingStatus.Rejected ||
                     r.ProcessingStatus == LabImportProcessingStatus.Failed));

            return Json(new
            {
                hasRecentImports = recentImportedCount > 0,
                recentImportedCount,
                recentRejectedCount,
                minutesWindow,
                connectorApiConfigured = !string.IsNullOrWhiteSpace(_labConnectorOptions.ApiKey),
                manualModeAvailable = true
            });
        }

        // GET: Laboratory/Manage/5 (Appointment ID)
        public async Task<IActionResult> Manage(int id)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.AppointmentItems)
                    .ThenInclude(ai => ai.Service)
                .Include(a => a.AppointmentItems)
                    .ThenInclude(ai => ai.LabResult)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appointment == null) return NotFound();

            return View(appointment);
        }

        // GET: Laboratory/Report?appointmentId=5
        public async Task<IActionResult> Report(int appointmentId)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor)
                .Include(a => a.AppointmentItems)
                    .ThenInclude(ai => ai.Service)
                        .ThenInclude(s => s.LabCategory)
                .Include(a => a.AppointmentItems)
                    .ThenInclude(ai => ai.LabResult)
                .FirstOrDefaultAsync(a => a.Id == appointmentId);

            if (appointment == null)
            {
                return NotFound();
            }

            var labItems = appointment.AppointmentItems
                .Where(ai => ai.Service != null && ai.Service.Type == ServiceType.LabTest)
                .ToList();

            if (!labItems.Any())
            {
                return NotFound();
            }

            var groupedRows = labItems
                .Select(ai =>
                {
                    var service = ai.Service;
                    var labResult = ai.LabResult;
                    var group = ResolveGroupNames(service);
                    var rawResult = labResult?.ResultValue;
                    var referenceRange = !string.IsNullOrWhiteSpace(labResult?.NormalRange)
                        ? labResult!.NormalRange!
                        : (service.NormalRange ?? "-");

                    return new
                    {
                        group.GroupNameAr,
                        group.GroupNameEn,
                        Row = new LabReportResultRowViewModel
                        {
                            TestNameAr = service.NameAr,
                            TestNameEn = !string.IsNullOrWhiteSpace(service.PrintName)
                                ? service.PrintName
                                : (string.IsNullOrWhiteSpace(service.NameEn) ? null : service.NameEn),
                            Reading = BuildReadingValue(labResult),
                            Flag = ResolveFlag(rawResult, referenceRange),
                            Unit = !string.IsNullOrWhiteSpace(labResult?.Unit) ? labResult!.Unit! : (service.Unit ?? "-"),
                            ReferenceRange = FormatReferenceRange(referenceRange, appointment.Patient?.Gender)
                        }
                    };
                })
                .GroupBy(x => new { x.GroupNameAr, x.GroupNameEn })
                .Select(g => new LabReportResultGroupViewModel
                {
                    GroupNameAr = g.Key.GroupNameAr,
                    GroupNameEn = g.Key.GroupNameEn,
                    Rows = g.Select(x => x.Row)
                        .OrderBy(r => r.TestNameEn ?? r.TestNameAr)
                        .ThenBy(r => r.TestNameAr)
                        .ToList()
                })
                .OrderBy(g => g.GroupNameEn)
                .ThenBy(g => g.GroupNameAr)
                .ToList();

            var latestPerformedAt = labItems
                .Where(ai => ai.LabResult?.PerformedAt != null)
                .Select(ai => ai.LabResult!.PerformedAt!.Value)
                .DefaultIfEmpty(DateTime.Now)
                .Max();

            var genderAr = appointment.Patient?.Gender == Gender.Female ? "أنثى" : "ذكر";
            var genderEn = appointment.Patient?.Gender == Gender.Female ? "Female" : "Male";
            var branding = _brandingService.GetBranding();
            var labNumber = BuildLabNumber(appointment);
            var reportDate = latestPerformedAt;
            var qrPayload = BuildDeterministicQrPayload(
                centerName: branding.CenterNameAr,
                patientName: appointment.Patient?.FullName ?? "-",
                patientId: appointment.PatientId,
                genderAr: genderAr,
                age: appointment.Patient?.Age ?? 0,
                doctorName: appointment.Doctor?.NameAr ?? "-",
                visitNumber: appointment.DailyNumber,
                labNumber: labNumber,
                reportDate: reportDate);

            var model = new LabReportPrintViewModel
            {
                Header = new LabReportHeaderViewModel
                {
                    CenterNameAr = branding.CenterNameAr,
                    CenterNameEn = branding.CenterNameEn,
                    LogoUrl = branding.LogoUrl
                },
                Patient = new LabReportPatientInfoViewModel
                {
                    PatientId = appointment.PatientId,
                    PatientName = appointment.Patient?.FullName ?? "-",
                    GenderAr = genderAr,
                    GenderEn = genderEn,
                    Age = appointment.Patient?.Age ?? 0,
                    Phone = appointment.Patient?.PhoneNumber ?? "-",
                    AttendingPhysician = appointment.Doctor?.NameAr ?? "-",
                    QrPayload = qrPayload,
                    QrCodeDataUri = _qrCodeService.GeneratePngDataUri(qrPayload, 5)
                },
                Meta = new LabReportMetaViewModel
                {
                    LabNumber = labNumber,
                    DailyNumber = appointment.DailyNumber,
                    AppointmentDate = appointment.AppointmentDate,
                    ReportDate = reportDate,
                    PrintedAt = DateTime.Now
                },
                Footer = new LabReportFooterViewModel
                {
                    AddressAr = branding.AddressAr,
                    Phone = branding.Phone
                },
                ResultGroups = groupedRows
            };

            return View(model);
        }

        // POST: Laboratory/UpdateResult
        [HttpPost]
        [Authorize(Roles = "LabTech,Doctor,Admin")]
        public async Task<IActionResult> UpdateResult(int id, string resultValue, string? unit, string? normalRange, LabStatus status, string? labNotes, string? performedBy)
        {
            var labResult = await _context.LabResults.FindAsync(id);
            if (labResult == null) return Json(new { success = false, message = "لم يتم العثور على الفحص" });

            labResult.ResultValue = resultValue;
            labResult.Unit = unit;
            labResult.NormalRange = normalRange;
            labResult.Status = status;
            labResult.LabNotes = labNotes;
            labResult.PerformedBy = performedBy;
            
            if (status == LabStatus.Completed)
            {
                labResult.PerformedAt = DateTime.Now;
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        // POST: /api/lab/import
        [AllowAnonymous]
        [HttpPost]
        [Route("/api/lab/import")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> ImportResult([FromBody] LabResultRequestPayload payload)
        {
            if (!Request.IsHttps && !_labConnectorOptions.AllowHttpInDevelopment)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    success = false,
                    message = "HTTPS is required for lab imports."
                });
            }

            if (!IsApiKeyAuthorized())
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Invalid connector API key."
                });
            }

            if (payload == null || payload.Results == null || payload.Results.Count == 0)
            {
                return BadRequest(new { success = false, message = "Invalid payload." });
            }

            var request = new LabResultsImportRequest
            {
                ConnectorSource = "LEGACY-API",
                Results = payload.Results
                    .Select(r => new NormalizedLabResultItem
                    {
                        DeviceId = payload.Device,
                        PatientIdentifier = payload.SampleId,
                        TestCode = r.Key,
                        ResultValue = r.Value,
                        Timestamp = DateTime.UtcNow,
                        RawPayload = JsonSerializer.Serialize(payload)
                    })
                    .ToList()
            };

            var result = await _labResultImportService.ImportAsync(request);
            return Ok(new
            {
                success = true,
                result
            });
        }

        private bool IsApiKeyAuthorized()
        {
            if (_environment.IsDevelopment() && _labConnectorOptions.AllowAnonymousInDevelopment)
            {
                return true;
            }

            var expectedKey = _labConnectorOptions.ApiKey?.Trim();
            if (string.IsNullOrWhiteSpace(expectedKey))
            {
                return false;
            }

            var headerName = string.IsNullOrWhiteSpace(_labConnectorOptions.ApiKeyHeaderName)
                ? "X-Connector-Api-Key"
                : _labConnectorOptions.ApiKeyHeaderName;

            if (!Request.Headers.TryGetValue(headerName, out var headerValue))
            {
                return false;
            }

            return string.Equals(headerValue.ToString().Trim(), expectedKey, StringComparison.Ordinal);
        }

    public class LabResultRequestPayload
    {
        public string Device { get; set; } = string.Empty;
        public string SampleId { get; set; } = string.Empty;
        public Dictionary<string, string> Results { get; set; } = new();
    }

        private static string BuildLabNumber(Appointment appointment)
        {
            return $"LAB-{appointment.AppointmentDate:yyyyMMdd}-{appointment.DailyNumber}";
        }

        private static string BuildDeterministicQrPayload(
            string centerName,
            string patientName,
            int patientId,
            string genderAr,
            int age,
            string doctorName,
            long visitNumber,
            string labNumber,
            DateTime reportDate)
        {
            var payload = new
            {
                center = centerName,
                patientName,
                patientId,
                gender = genderAr,
                age,
                doctor = doctorName,
                visitNumber,
                labNumber,
                reportDate = reportDate.ToString("yyyy-MM-dd HH:mm")
            };

            return JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            });
        }

        private static string BuildReadingValue(LabResult? result)
        {
            if (result == null)
            {
                return "-";
            }

            if (!string.IsNullOrWhiteSpace(result.ResultValue))
            {
                return result.ResultValue!;
            }

            return result.Status switch
            {
                LabStatus.Pending => "Pending / قيد الانتظار",
                LabStatus.InProgress => "In Progress / قيد التنفيذ",
                LabStatus.Cancelled => "Cancelled / ملغي",
                _ => "-"
            };
        }

        private static (string GroupNameAr, string GroupNameEn) ResolveGroupNames(ClinicService service)
        {
            if (service.LabCategory != null)
            {
                return (service.LabCategory.NameAr, service.LabCategory.NameEn ?? service.LabCategory.NameAr);
            }

            return ("تحاليل عامة", "General Tests");
        }

        private static string ResolveFlag(string? rawResult, string? rawRange)
        {
            if (!TryParseNumber(rawResult, out var reading))
            {
                return string.Empty;
            }

            if (!TryExtractRange(rawRange, out var min, out var max, out var hasMin, out var hasMax))
            {
                return string.Empty;
            }

            if (hasMin && reading < min)
            {
                return "L";
            }

            if (hasMax && reading > max)
            {
                return "H";
            }

            return "N";
        }

        private static bool TryExtractRange(string? text, out decimal min, out decimal max, out bool hasMin, out bool hasMax)
        {
            min = 0;
            max = 0;
            hasMin = false;
            hasMax = false;

            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var normalized = text.Trim().ToLowerInvariant();

            var rangeSeparators = new[] { "-", "–", "—" };
            foreach (var separator in rangeSeparators)
            {
                if (normalized.Contains(separator))
                {
                    var parts = normalized.Split(separator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (parts.Length == 2 &&
                        TryParseNumber(parts[0], out var fromValue) &&
                        TryParseNumber(parts[1], out var toValue))
                    {
                        min = Math.Min(fromValue, toValue);
                        max = Math.Max(fromValue, toValue);
                        hasMin = true;
                        hasMax = true;
                        return true;
                    }
                }
            }

            if (normalized.StartsWith("up to", StringComparison.OrdinalIgnoreCase) || normalized.Contains("حتى"))
            {
                if (TryParseNumber(normalized, out var upperValue))
                {
                    max = upperValue;
                    hasMax = true;
                    return true;
                }
            }

            if ((normalized.Contains("<=") || normalized.Contains("<")) && TryParseNumber(normalized, out var lessValue))
            {
                max = lessValue;
                hasMax = true;
                return true;
            }

            if ((normalized.Contains(">=") || normalized.Contains(">")) && TryParseNumber(normalized, out var greaterValue))
            {
                min = greaterValue;
                hasMin = true;
                return true;
            }

            return false;
        }

        private static bool TryParseNumber(string? text, out decimal value)
        {
            value = 0m;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var cleaned = new string(text.Where(c => char.IsDigit(c) || c == '.' || c == ',' || c == '-').ToArray());
            if (string.IsNullOrWhiteSpace(cleaned))
            {
                return false;
            }

            if (decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }

            if (decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.GetCultureInfo("ar-EG"), out value))
            {
                return true;
            }

            return decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.CurrentCulture, out value);
        }

        private static string FormatReferenceRange(string? rawRange, Gender? gender)
        {
            if (string.IsNullOrWhiteSpace(rawRange) || rawRange == "-")
                return "-";

            var range = rawRange.Trim();

            if (!gender.HasValue) 
                return range;

            // Handle standard split "Male: ... / Female: ..."
            if (range.Contains("/") || range.Contains(" / "))
            {
                var parts = range.Split(new[] { "/", " / " }, StringSplitOptions.RemoveEmptyEntries);
                foreach (var part in parts)
                {
                    var p = part.Trim();
                    var isMalePart = p.StartsWith("Male", StringComparison.OrdinalIgnoreCase);
                    var isFemalePart = p.StartsWith("Female", StringComparison.OrdinalIgnoreCase);

                    if (gender == Gender.Male && isMalePart)
                        return CleanRange(p, "Male");
                    
                    if (gender == Gender.Female && isFemalePart)
                        return CleanRange(p, "Female");
                }
            }

            return range;
        }

        private static string CleanRange(string part, string prefix)
        {
            // Try to remove "prefix:" or "prefix :" 
            var idx = part.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                var clean = part.Substring(idx + prefix.Length).Trim();
                if (clean.StartsWith(":")) 
                {
                    clean = clean.Substring(1).Trim();
                }
                return clean;
            }
            return part;
        }
    }
}

