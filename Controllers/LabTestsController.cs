using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NagmClinic.Data;
using NagmClinic.Models;
using NagmClinic.Models.Enums;
using NagmClinic.ViewModels;
using NagmClinic.Extensions;
using NagmClinic.Models.DataTables;
using NagmClinic.Services.Laboratory;

namespace NagmClinic.Controllers
{
    public class LabTestsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILabCatalogSeedService _labCatalogSeedService;

        public LabTestsController(ApplicationDbContext context, ILabCatalogSeedService labCatalogSeedService)
        {
            _context = context;
            _labCatalogSeedService = labCatalogSeedService;
        }

        // GET: LabTests
        public async Task<IActionResult> Index()
        {
            await PopulateLookupDataAsync();
            return View(await BuildIndexViewModelAsync());
        }

        [HttpPost]
        public async Task<IActionResult> GetLabTestsData()
        {
            try
            {
                var dtParams = Request.GetDataTablesParameters();
                var query = _context.ClinicServices
                    .Include(s => s.LabCategory)
                    .Include(s => s.LabAnalyzer)
                    .Where(s => s.Type == ServiceType.LabTest);

                var searchValue = dtParams.Search != null && dtParams.Search.ContainsKey("value") ? dtParams.Search["value"] : null;
                if (!string.IsNullOrEmpty(searchValue))
                {
                    query = query.Where(s =>
                        s.NameAr.Contains(searchValue) ||
                        s.NameEn.Contains(searchValue) ||
                        (s.Code != null && s.Code.Contains(searchValue)) ||
                        (s.DeviceCode != null && s.DeviceCode.Contains(searchValue)));
                }

                int recordsTotal = await query.CountAsync();
                var data = await query
                    .OrderBy(s => s.LabCategory != null ? s.LabCategory.SortOrder : 999)
                    .ThenBy(s => s.SortOrder)
                    .ThenBy(s => s.NameAr)
                    .Skip(dtParams.Start)
                    .Take(dtParams.Length)
                    .Select(s => new
                    {
                        s.Id,
                        s.NameAr,
                        s.NameEn,
                        s.Code,
                        s.PrintName,
                        s.Price,
                        s.Unit,
                        s.NormalRange,
                        s.ReferenceRange,
                        s.CriticalRange,
                        ResultType = (int)s.ResultType,
                        SourceType = (int)s.SourceType,
                        s.PredefinedValues,
                        s.Notes,
                        s.IsActive,
                        s.IsDeviceMapped,
                        s.DeviceCode,
                        s.SampleType,
                        s.SortOrder,
                        s.LabCategoryId,
                        s.LabAnalyzerId,
                        CategoryName = s.LabCategory != null ? s.LabCategory.NameAr : "-",
                        AnalyzerName = s.LabAnalyzer != null ? s.LabAnalyzer.Name : "-"
                    })
                    .ToListAsync();
                return Json(new DataTablesResponse<object> { draw = dtParams.Draw, recordsTotal = recordsTotal, recordsFiltered = recordsTotal, data = data });
            }
            catch (Exception ex) { return Json(new DataTablesResponse<object> { error = ex.Message }); }
        }

        // AJAX POST: LabTests/QuickCreate
        [HttpPost]
        public async Task<IActionResult> QuickCreate([FromBody] ClinicServiceViewModel model)
        {
            if (ModelState.IsValid)
            {
                var normalizedCode = NormalizeNullable(model.Code);
                if (await HasConflictingCodeAsync(normalizedCode))
                {
                    return Json(new { success = false, message = "كود الفحص مستخدم بالفعل" });
                }

                var validationMessage = ValidateDeviceMapping(model);
                if (validationMessage != null)
                {
                    return Json(new { success = false, message = validationMessage });
                }

                var normalizedDeviceCode = model.SourceType == LabTestSourceType.Manual
                    ? null
                    : NormalizeNullable(model.DeviceCode) ?? normalizedCode;

                var test = new ClinicService
                {
                    NameAr = model.NameAr,
                    NameEn = model.NameEn,
                    Code = normalizedCode,
                    Type = ServiceType.LabTest, // Force Type to LabTest
                    Price = model.Price,
                    Notes = model.Notes,
                    Unit = model.Unit,
                    NormalRange = model.NormalRange,
                    ReferenceRange = model.ReferenceRange,
                    CriticalRange = model.CriticalRange,
                    SampleType = model.SampleType,
                    ResultType = model.ResultType,
                    SourceType = model.SourceType,
                    IsDeviceMapped = model.SourceType != LabTestSourceType.Manual,
                    DeviceCode = normalizedDeviceCode,
                    PredefinedValues = NormalizePredefinedValues(model.ResultType, model.PredefinedValues),
                    SortOrder = model.SortOrder,
                    LabCategoryId = model.LabCategoryId,
                    LabAnalyzerId = model.SourceType == LabTestSourceType.Manual ? null : model.LabAnalyzerId,
                    PrintName = NormalizeNullable(model.PrintName) ?? NormalizeNullable(model.NameEn) ?? model.NameAr,
                    IsActive = true
                };

                _context.ClinicServices.Add(test);
                await _context.SaveChangesAsync();
                return Json(new { success = true, testId = test.Id, name = test.NameAr, price = test.Price });
            }

            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            return Json(new { success = false, message = string.Join(", ", errors) });
        }

        // AJAX POST: LabTests/QuickEdit
        [HttpPost]
        public async Task<IActionResult> QuickEdit([FromBody] ClinicServiceViewModel model)
        {
            if (ModelState.IsValid)
            {
                var test = await _context.ClinicServices.FindAsync(model.Id);
                if (test == null || test.Type != ServiceType.LabTest) 
                    return Json(new { success = false, message = "لم يتم العثور على الفحص المعملي" });

                var normalizedCode = NormalizeNullable(model.Code);
                if (await HasConflictingCodeAsync(normalizedCode, model.Id))
                {
                    return Json(new { success = false, message = "كود الفحص مستخدم بالفعل" });
                }

                var validationMessage = ValidateDeviceMapping(model);
                if (validationMessage != null)
                {
                    return Json(new { success = false, message = validationMessage });
                }

                var normalizedDeviceCode = model.SourceType == LabTestSourceType.Manual
                    ? null
                    : NormalizeNullable(model.DeviceCode) ?? normalizedCode;

                test.NameAr = model.NameAr;
                test.NameEn = model.NameEn;
                test.Code = normalizedCode;
                test.Price = model.Price;
                test.Unit = model.Unit;
                test.NormalRange = model.NormalRange;
                test.ReferenceRange = model.ReferenceRange;
                test.CriticalRange = model.CriticalRange;
                test.SampleType = model.SampleType;
                test.ResultType = model.ResultType;
                test.SourceType = model.SourceType;
                test.IsDeviceMapped = model.SourceType != LabTestSourceType.Manual;
                test.DeviceCode = normalizedDeviceCode;
                test.PredefinedValues = NormalizePredefinedValues(model.ResultType, model.PredefinedValues);
                test.SortOrder = model.SortOrder;
                test.LabCategoryId = model.LabCategoryId;
                test.LabAnalyzerId = model.SourceType == LabTestSourceType.Manual ? null : model.LabAnalyzerId;
                test.Notes = model.Notes;
                test.PrintName = NormalizeNullable(model.PrintName) ?? NormalizeNullable(model.NameEn) ?? model.NameAr;
                test.IsActive = model.IsActive;

                _context.Update(test);
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }

            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            return Json(new { success = false, message = string.Join(", ", errors) });
        }

        // GET: LabTests/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var test = await _context.ClinicServices.FindAsync(id);
            if (test == null || test.Type != ServiceType.LabTest) return NotFound();

            var model = new ClinicServiceViewModel
            {
                Id = test.Id,
                NameAr = test.NameAr,
                NameEn = test.NameEn,
                Code = test.Code,
                Type = test.Type,
                Price = test.Price,
                Notes = test.Notes,
                Unit = test.Unit,
                NormalRange = test.NormalRange,
                ReferenceRange = test.ReferenceRange,
                CriticalRange = test.CriticalRange,
                PrintName = test.PrintName,
                SampleType = test.SampleType,
                ResultType = test.ResultType,
                PredefinedValues = test.PredefinedValues,
                SourceType = test.SourceType,
                DeviceCode = test.DeviceCode,
                SortOrder = test.SortOrder,
                LabCategoryId = test.LabCategoryId,
                LabAnalyzerId = test.LabAnalyzerId,
                IsActive = test.IsActive
            };

            await PopulateLookupDataAsync();
            return View(model);
        }

        // POST: LabTests/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ClinicServiceViewModel model)
        {
            if (id != model.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var test = await _context.ClinicServices.FindAsync(id);
                    if (test == null) return NotFound();

                    var normalizedCode = NormalizeNullable(model.Code);
                    if (await HasConflictingCodeAsync(normalizedCode, model.Id))
                    {
                        ModelState.AddModelError(nameof(model.Code), "كود الفحص مستخدم بالفعل.");
                        await PopulateLookupDataAsync();
                        return View(model);
                    }

                    var validationMessage = ValidateDeviceMapping(model);
                    if (validationMessage != null)
                    {
                        ModelState.AddModelError(string.Empty, validationMessage);
                        await PopulateLookupDataAsync();
                        return View(model);
                    }

                    var normalizedDeviceCode = model.SourceType == LabTestSourceType.Manual
                        ? null
                        : NormalizeNullable(model.DeviceCode) ?? normalizedCode;

                    test.NameAr = model.NameAr;
                    test.NameEn = model.NameEn;
                    test.Code = normalizedCode;
                    test.Price = model.Price;
                    test.Notes = model.Notes;
                    test.Unit = model.Unit;
                    test.NormalRange = model.NormalRange;
                    test.ReferenceRange = model.ReferenceRange;
                    test.CriticalRange = model.CriticalRange;
                    test.SampleType = model.SampleType;
                    test.ResultType = model.ResultType;
                    test.SourceType = model.SourceType;
                    test.IsDeviceMapped = model.SourceType != LabTestSourceType.Manual;
                    test.DeviceCode = normalizedDeviceCode;
                    test.PredefinedValues = NormalizePredefinedValues(model.ResultType, model.PredefinedValues);
                    test.SortOrder = model.SortOrder;
                    test.LabCategoryId = model.LabCategoryId;
                    test.LabAnalyzerId = model.SourceType == LabTestSourceType.Manual ? null : model.LabAnalyzerId;
                    test.PrintName = NormalizeNullable(model.PrintName) ?? NormalizeNullable(model.NameEn) ?? model.NameAr;
                    test.IsActive = model.IsActive;

                    _context.Update(test);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!TestExists(model.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            await PopulateLookupDataAsync();
            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> SeedEc38Catalog()
        {
            var added = await _labCatalogSeedService.SeedEc38TestsAsync();
            return Json(new
            {
                success = true,
                message = added > 0
                    ? $"تمت إضافة {added} فحص من جهاز EC-38."
                    : "فحوصات EC-38 موجودة بالفعل."
            });
        }

        // AJAX POST: LabTests/ToggleStatus/5
        [HttpPost]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var test = await _context.ClinicServices.FindAsync(id);
            if (test == null) return Json(new { success = false });

            test.IsActive = !test.IsActive;
            await _context.SaveChangesAsync();
            return Json(new { success = true, isActive = test.IsActive });
        }

        private bool TestExists(int id)
        {
            return _context.ClinicServices.Any(e => e.Id == id);
        }

        private async Task<LabTestsIndexViewModel> BuildIndexViewModelAsync()
        {
            return new LabTestsIndexViewModel
            {
                Categories = await _context.LabCategories
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.SortOrder)
                    .ThenBy(c => c.NameAr)
                    .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.NameAr })
                    .ToListAsync(),
                Analyzers = await _context.LabAnalyzers
                    .Where(a => a.IsActive)
                    .OrderBy(a => a.Name)
                    .Select(a => new SelectListItem { Value = a.Id.ToString(), Text = a.Name })
                    .ToListAsync()
            };
        }

        private async Task PopulateLookupDataAsync()
        {
            ViewBag.LabCategories = await _context.LabCategories
                .Where(c => c.IsActive)
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.NameAr)
                .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.NameAr })
                .ToListAsync();

            ViewBag.LabAnalyzers = await _context.LabAnalyzers
                .Where(a => a.IsActive)
                .OrderBy(a => a.Name)
                .Select(a => new SelectListItem { Value = a.Id.ToString(), Text = a.Name })
                .ToListAsync();
        }

        private async Task<bool> HasConflictingCodeAsync(string? code, int? currentId = null)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                return false;
            }

            return await _context.ClinicServices.AnyAsync(s =>
                s.Type == ServiceType.LabTest &&
                s.Code == code &&
                (!currentId.HasValue || s.Id != currentId.Value));
        }

        private static string? NormalizeNullable(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string? NormalizePredefinedValues(LabResultType resultType, string? predefinedValues)
        {
            if (resultType == LabResultType.PositiveNegative)
            {
                return "Positive,Negative";
            }

            return NormalizeNullable(predefinedValues);
        }

        private static string? ValidateDeviceMapping(ClinicServiceViewModel model)
        {
            if (model.SourceType == LabTestSourceType.Manual)
            {
                return null;
            }

            if (!model.LabAnalyzerId.HasValue || model.LabAnalyzerId.Value <= 0)
            {
                return "يرجى اختيار الجهاز للفحص المربوط آلياً.";
            }

            if (string.IsNullOrWhiteSpace(model.DeviceCode) && string.IsNullOrWhiteSpace(model.Code))
            {
                return "يرجى إدخال كود الفحص أو كود الربط بالجهاز.";
            }

            return null;
        }
    }
}
