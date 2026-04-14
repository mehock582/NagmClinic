using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using NagmClinic.Data;
using NagmClinic.Extensions;
using NagmClinic.Models;
using NagmClinic.Models.DataTables;
using NagmClinic.Models.Enums;
using NagmClinic.ViewModels;

namespace NagmClinic.Controllers
{
    public class LabDeviceMappingsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public LabDeviceMappingsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            return View(await BuildIndexViewModelAsync());
        }

        [HttpPost]
        public async Task<IActionResult> GetMappingsData()
        {
            try
            {
                var dtParams = Request.GetDataTablesParameters();
                var searchValue = dtParams.Search.TryGetValue("value", out var value) ? value : null;

                var query = _context.LabDeviceTestMappings
                    .Include(m => m.LabTest)
                    .Where(m => m.LabTest.Type == ServiceType.LabTest)
                    .AsQueryable();

                if (!string.IsNullOrWhiteSpace(searchValue))
                {
                    query = query.Where(m =>
                        m.DeviceId.Contains(searchValue) ||
                        m.DeviceTestCode.Contains(searchValue) ||
                        m.LabTest.NameAr.Contains(searchValue) ||
                        (m.LabTest.Code != null && m.LabTest.Code.Contains(searchValue)));
                }

                var data = await query
                    .OrderBy(m => m.DeviceId)
                    .ThenBy(m => m.DeviceTestCode)
                    .Skip(dtParams.Start)
                    .Take(dtParams.Length)
                    .Select(m => new
                    {
                        m.Id,
                        m.DeviceId,
                        DeviceName = _context.LabAnalyzers
                            .Where(a => a.Code == m.DeviceId)
                            .Select(a => a.Name)
                            .FirstOrDefault() ?? m.DeviceId,
                        m.DeviceTestCode,
                        m.LabTestId,
                        LabTestCode = m.LabTest.Code,
                        LabTestName = m.LabTest.NameAr,
                        m.IsActive
                    })
                    .ToListAsync();

                var total = await query.CountAsync();
                return Json(new DataTablesResponse<object>
                {
                    draw = dtParams.Draw,
                    recordsTotal = total,
                    recordsFiltered = total,
                    data = data
                });
            }
            catch (Exception ex)
            {
                return Json(new DataTablesResponse<object> { error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> QuickCreate([FromBody] LabDeviceMappingInputViewModel model)
        {
            var validation = await ValidateInputAsync(model);
            if (!validation.Success)
            {
                return Json(new { success = false, message = validation.Message });
            }

            var exists = await _context.LabDeviceTestMappings
                .AnyAsync(m => m.DeviceId == validation.DeviceId && m.DeviceTestCode == validation.DeviceTestCode);
            if (exists)
            {
                return Json(new { success = false, message = "هذا الربط موجود مسبقاً لنفس الجهاز والكود." });
            }

            var mapping = new LabDeviceTestMapping
            {
                DeviceId = validation.DeviceId!,
                DeviceTestCode = validation.DeviceTestCode!,
                LabTestId = model.LabTestId,
                IsActive = true
            };

            _context.LabDeviceTestMappings.Add(mapping);
            await _context.SaveChangesAsync();

            return Json(new { success = true, id = mapping.Id });
        }

        [HttpPost]
        public async Task<IActionResult> QuickEdit([FromBody] LabDeviceMappingInputViewModel model)
        {
            var mapping = await _context.LabDeviceTestMappings.FindAsync(model.Id);
            if (mapping == null)
            {
                return Json(new { success = false, message = "لم يتم العثور على الربط المطلوب." });
            }

            var validation = await ValidateInputAsync(model);
            if (!validation.Success)
            {
                return Json(new { success = false, message = validation.Message });
            }

            var exists = await _context.LabDeviceTestMappings
                .AnyAsync(m =>
                    m.Id != model.Id &&
                    m.DeviceId == validation.DeviceId &&
                    m.DeviceTestCode == validation.DeviceTestCode);
            if (exists)
            {
                return Json(new { success = false, message = "هذا الربط موجود مسبقاً لنفس الجهاز والكود." });
            }

            mapping.DeviceId = validation.DeviceId!;
            mapping.DeviceTestCode = validation.DeviceTestCode!;
            mapping.LabTestId = model.LabTestId;
            mapping.IsActive = model.IsActive;
            mapping.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var mapping = await _context.LabDeviceTestMappings.FindAsync(id);
            if (mapping == null)
            {
                return Json(new { success = false });
            }

            mapping.IsActive = !mapping.IsActive;
            mapping.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Json(new { success = true, isActive = mapping.IsActive });
        }

        [HttpGet("/api/lab-device-mappings")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> GetMappingsApi([FromQuery] string? deviceId = null, [FromQuery] bool activeOnly = true)
        {
            var normalizedDeviceId = Normalize(deviceId);
            var query = _context.LabDeviceTestMappings
                .Include(m => m.LabTest)
                .Where(m => m.LabTest.Type == ServiceType.LabTest)
                .AsQueryable();

            if (activeOnly)
            {
                query = query.Where(m => m.IsActive);
            }

            if (!string.IsNullOrWhiteSpace(normalizedDeviceId))
            {
                query = query.Where(m => m.DeviceId == normalizedDeviceId);
            }

            var mappings = await query
                .OrderBy(m => m.DeviceId)
                .ThenBy(m => m.DeviceTestCode)
                .Select(m => new
                {
                    m.Id,
                    m.DeviceId,
                    m.DeviceTestCode,
                    m.LabTestId,
                    LabTestCode = m.LabTest.Code,
                    LabTestName = m.LabTest.NameAr,
                    m.IsActive,
                    m.CreatedAt,
                    m.UpdatedAt
                })
                .ToListAsync();

            return Ok(mappings);
        }

        [HttpPost("/api/lab-device-mappings")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> CreateMappingApi([FromBody] LabDeviceMappingInputViewModel model)
        {
            var validation = await ValidateInputAsync(model);
            if (!validation.Success)
            {
                return BadRequest(new { message = validation.Message });
            }

            var exists = await _context.LabDeviceTestMappings
                .AnyAsync(m => m.DeviceId == validation.DeviceId && m.DeviceTestCode == validation.DeviceTestCode);
            if (exists)
            {
                return Conflict(new { message = "Mapping already exists for this DeviceId and DeviceTestCode." });
            }

            var mapping = new LabDeviceTestMapping
            {
                DeviceId = validation.DeviceId!,
                DeviceTestCode = validation.DeviceTestCode!,
                LabTestId = model.LabTestId,
                IsActive = model.IsActive
            };

            _context.LabDeviceTestMappings.Add(mapping);
            await _context.SaveChangesAsync();
            return Created($"/api/lab-device-mappings/{mapping.Id}", new { mapping.Id });
        }

        [HttpPut("/api/lab-device-mappings/{id:int}")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> UpdateMappingApi(int id, [FromBody] LabDeviceMappingInputViewModel model)
        {
            var mapping = await _context.LabDeviceTestMappings.FindAsync(id);
            if (mapping == null)
            {
                return NotFound();
            }

            model.Id = id;
            var validation = await ValidateInputAsync(model);
            if (!validation.Success)
            {
                return BadRequest(new { message = validation.Message });
            }

            var exists = await _context.LabDeviceTestMappings
                .AnyAsync(m =>
                    m.Id != id &&
                    m.DeviceId == validation.DeviceId &&
                    m.DeviceTestCode == validation.DeviceTestCode);
            if (exists)
            {
                return Conflict(new { message = "Mapping already exists for this DeviceId and DeviceTestCode." });
            }

            mapping.DeviceId = validation.DeviceId!;
            mapping.DeviceTestCode = validation.DeviceTestCode!;
            mapping.LabTestId = model.LabTestId;
            mapping.IsActive = model.IsActive;
            mapping.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [HttpPatch("/api/lab-device-mappings/{id:int}/status")]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> SetMappingStatusApi(int id, [FromBody] bool isActive)
        {
            var mapping = await _context.LabDeviceTestMappings.FindAsync(id);
            if (mapping == null)
            {
                return NotFound();
            }

            mapping.IsActive = isActive;
            mapping.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private async Task<LabDeviceMappingsIndexViewModel> BuildIndexViewModelAsync()
        {
            var devices = await _context.LabAnalyzers
                .Where(a => a.IsActive && a.Code != null)
                .OrderBy(a => a.Name)
                .Select(a => new SelectListItem
                {
                    Value = a.Code!,
                    Text = $"{a.Name} ({a.Code})"
                })
                .ToListAsync();

            var tests = await _context.ClinicServices
                .Where(s => s.Type == ServiceType.LabTest && s.IsActive)
                .OrderBy(s => s.Code)
                .ThenBy(s => s.NameAr)
                .Select(s => new SelectListItem
                {
                    Value = s.Id.ToString(),
                    Text = string.IsNullOrWhiteSpace(s.Code) ? s.NameAr : $"{s.Code} - {s.NameAr}"
                })
                .ToListAsync();

            return new LabDeviceMappingsIndexViewModel
            {
                DeviceOptions = devices,
                LabTests = tests
            };
        }

        private async Task<(bool Success, string Message, string? DeviceId, string? DeviceTestCode)> ValidateInputAsync(
            LabDeviceMappingInputViewModel model)
        {
            var deviceId = Normalize(model.DeviceId);
            if (string.IsNullOrWhiteSpace(deviceId))
            {
                return (false, "يرجى اختيار الجهاز.", null, null);
            }

            var analyzerExists = await _context.LabAnalyzers
                .AnyAsync(a => a.IsActive && a.Code == deviceId);
            if (!analyzerExists)
            {
                return (false, "الجهاز المحدد غير موجود أو غير مفعل.", null, null);
            }

            var deviceTestCode = Normalize(model.DeviceTestCode);
            if (string.IsNullOrWhiteSpace(deviceTestCode))
            {
                return (false, "يرجى إدخال كود الفحص القادم من الجهاز.", null, null);
            }

            var labTestExists = await _context.ClinicServices
                .AnyAsync(s => s.Id == model.LabTestId && s.Type == ServiceType.LabTest);
            if (!labTestExists)
            {
                return (false, "الفحص المعملي المحدد غير صالح.", null, null);
            }

            return (true, string.Empty, deviceId, deviceTestCode);
        }

        private static string? Normalize(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();
        }
    }
}
