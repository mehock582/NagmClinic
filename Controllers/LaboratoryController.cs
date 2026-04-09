using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NagmClinic.Data;
using NagmClinic.Models;
using NagmClinic.Models.Enums;
using NagmClinic.ViewModels;
using NagmClinic.Extensions;
using NagmClinic.Models.DataTables;
using NagmClinic.Services.Branding;
using NagmClinic.Services.Reports;

namespace NagmClinic.Controllers
{
    public class LaboratoryController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IClinicBrandingService _brandingService;
        private readonly IQrCodeService _qrCodeService;

        public LaboratoryController(
            ApplicationDbContext context,
            IClinicBrandingService brandingService,
            IQrCodeService qrCodeService)
        {
            _context = context;
            _brandingService = brandingService;
            _qrCodeService = qrCodeService;
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

                var searchValue = dtParams.Search != null && dtParams.Search.ContainsKey("value") ? dtParams.Search["value"] : null;
                if (!string.IsNullOrEmpty(searchValue))
                {
                    query = query.Where(lr => (lr.AppointmentItem.Appointment.Patient != null &&
                                               lr.AppointmentItem.Appointment.Patient.FullName.Contains(searchValue)) ||
                                           lr.AppointmentItem.Appointment.DailyNumber.ToString().Contains(searchValue));
                }

                int recordsTotal = await query.CountAsync();
                var data = await query
                    .OrderByDescending(lr => lr.AppointmentItem.Appointment.AppointmentDate)
                    .Skip(dtParams.Start).Take(dtParams.Length)
                    .Select(lr => new
                    {
                        lr.Id,
                        AppointmentId = (lr.AppointmentItem != null) ? lr.AppointmentItem.AppointmentId : 0,
                        DailyNumber = (lr.AppointmentItem != null && lr.AppointmentItem.Appointment != null) ? lr.AppointmentItem.Appointment.DailyNumber.ToString() : "-",
                        AppointmentDate = (lr.AppointmentItem != null && lr.AppointmentItem.Appointment != null) ? lr.AppointmentItem.Appointment.AppointmentDate.ToString("yyyy-MM-dd HH:mm") : "-",
                        PatientName = (lr.AppointmentItem != null && lr.AppointmentItem.Appointment != null && lr.AppointmentItem.Appointment.Patient != null) ? lr.AppointmentItem.Appointment.Patient.FullName : "غير معروف",
                        PatientPhone = (lr.AppointmentItem != null && lr.AppointmentItem.Appointment != null && lr.AppointmentItem.Appointment.Patient != null) ? lr.AppointmentItem.Appointment.Patient.PhoneNumber : "-",
                        DoctorName = (lr.AppointmentItem != null && lr.AppointmentItem.Appointment != null && lr.AppointmentItem.Appointment.Doctor != null) ? lr.AppointmentItem.Appointment.Doctor.NameAr : "",
                        TestName = (lr.AppointmentItem != null && lr.AppointmentItem.Service != null) ? lr.AppointmentItem.Service.NameAr : "-",
                        ResultType = (lr.AppointmentItem != null && lr.AppointmentItem.Service != null) ? (int)lr.AppointmentItem.Service.ResultType : 0,
                        PredefinedValues = (lr.AppointmentItem != null && lr.AppointmentItem.Service != null) ? lr.AppointmentItem.Service.PredefinedValues : "",
                        Status = lr.Status.ToString(),
                        lr.ResultValue,
                        lr.Unit,
                        lr.NormalRange,
                        lr.LabNotes,
                        lr.PerformedBy
                    })
                    .ToListAsync();

                return Json(new DataTablesResponse<object> { draw = dtParams.Draw, recordsTotal = recordsTotal, recordsFiltered = recordsTotal, data = data });
            }
            catch (Exception ex) { return Json(new DataTablesResponse<object> { error = ex.Message }); }
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
                            TestNameEn = string.IsNullOrWhiteSpace(service.NameEn) ? null : service.NameEn,
                            Reading = BuildReadingValue(labResult),
                            Flag = ResolveFlag(rawResult, referenceRange),
                            Unit = !string.IsNullOrWhiteSpace(labResult?.Unit) ? labResult!.Unit! : (service.Unit ?? "-"),
                            ReferenceRange = referenceRange
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
        [HttpPost]
        [Route("/api/lab/import")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> ImportResult([FromBody] LabResultRequestPayload payload)
        {
            try
            {
                if (payload == null || payload.Results == null || !payload.Results.Any())
                {
                    return BadRequest("Invalid payload.");
                }

                long appointmentId = -1;
                long dailyNumber = -1;
                if (payload.SampleId.StartsWith("LAB-"))
                {
                    var parts = payload.SampleId.Split('-');
                    if (parts.Length >= 3 && long.TryParse(parts[2], out var number))
                    {
                        dailyNumber = number;
                    }
                } 
                else if (long.TryParse(payload.SampleId, out var parsed))
                {
                    dailyNumber = parsed;
                    appointmentId = parsed;
                }

                var query = _context.Appointments
                    .Include(a => a.AppointmentItems)
                        .ThenInclude(ai => ai.Service)
                    .Include(a => a.AppointmentItems)
                        .ThenInclude(ai => ai.LabResult)
                    .AsQueryable();

                var appointment = await query.FirstOrDefaultAsync(a => 
                    (a.DailyNumber == dailyNumber && a.AppointmentDate.Date == DateTime.Today.Date) || 
                    a.Id == appointmentId);

                if (appointment == null)
                {
                    return NotFound(new { success = false, message = $"Could not find appointment for Sample ID: {payload.SampleId}" });
                }

                int updatedCount = 0;
                foreach (var kvp in payload.Results)
                {
                    var testCode = kvp.Key.Trim();
                    var testValue = kvp.Value;

                    // Match logic: Exact match OR Name contains the code OR Code contains the name
                    // Important: Ignore null or empty strings to prevent false positives!
                    var appointmentItem = appointment.AppointmentItems.FirstOrDefault(ai => 
                    {
                        if (ai.Service == null) return false;
                        var en = ai.Service.NameEn?.Trim();
                        var ar = ai.Service.NameAr?.Trim();
                        bool validEn = !string.IsNullOrEmpty(en);
                        bool validAr = !string.IsNullOrEmpty(ar);

                        return (validEn && string.Equals(en, testCode, StringComparison.OrdinalIgnoreCase)) ||
                               (validAr && string.Equals(ar, testCode, StringComparison.OrdinalIgnoreCase)) ||
                               (validEn && en!.Contains(testCode, StringComparison.OrdinalIgnoreCase)) ||
                               (validAr && ar!.Contains(testCode, StringComparison.OrdinalIgnoreCase)) ||
                               (validEn && testCode.Contains(en!, StringComparison.OrdinalIgnoreCase)) ||
                               (validAr && testCode.Contains(ar!, StringComparison.OrdinalIgnoreCase));
                    });
                    if (appointmentItem != null && appointmentItem.LabResult != null)
                    {
                        appointmentItem.LabResult.ResultValue = testValue;
                        appointmentItem.LabResult.Status = LabStatus.Completed;
                        appointmentItem.LabResult.LabNotes = $"Auto-Imported from {payload.Device} at {DateTime.Now:HH:mm}";
                        appointmentItem.LabResult.PerformedBy = payload.Device;
                        appointmentItem.LabResult.PerformedAt = DateTime.Now;
                        updatedCount++;
                    }
                }

                await _context.SaveChangesAsync();

                return Ok(new { success = true, updatedCount });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { success = false, message = ex.Message });
            }
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
            var text = $"{service.NameAr} {service.NameEn}".ToLowerInvariant();

            if (ContainsAny(text, "renal", "creatinine", "urea", "electrolyte", "s.na", "s.k", "وظائف الكلى", "كرياتينين", "يوريا", "صوديوم", "بوتاسيوم"))
            {
                return ("وظائف الكلى", "Renal Function Tests");
            }

            if (ContainsAny(text, "cbc", "hematology", "wbc", "rbc", "hb", "hct", "platelet", "صورة دم", "هيموجلوبين", "صفائح"))
            {
                return ("صورة الدم", "CBC / Hematology");
            }

            if (ContainsAny(text, "liver", "alt", "ast", "bilirubin", "alp", "وظائف الكبد", "بيليروبين"))
            {
                return ("وظائف الكبد", "Liver Function");
            }

            if (ContainsAny(text, "chemistry", "glucose", "sugar", "cholesterol", "triglyceride", "كيمياء", "سكر", "كوليسترول"))
            {
                return ("الكيمياء", "Chemistry");
            }

            return ("تحاليل عامة", "General Tests");
        }

        private static bool ContainsAny(string source, params string[] terms)
        {
            return terms.Any(t => source.Contains(t, StringComparison.OrdinalIgnoreCase));
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
    }
}

