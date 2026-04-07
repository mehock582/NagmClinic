using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NagmClinic.Data;
using NagmClinic.Models;
using NagmClinic.Models.Enums;
using NagmClinic.ViewModels;
using NagmClinic.Extensions;
using NagmClinic.Models.DataTables;

namespace NagmClinic.Controllers
{
    public class LabTestsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public LabTestsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: LabTests
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> GetLabTestsData()
        {
            try
            {
                var dtParams = Request.GetDataTablesParameters();
                var query = _context.ClinicServices.Where(s => s.Type == ServiceType.LabTest);
                var searchValue = dtParams.Search != null && dtParams.Search.ContainsKey("value") ? dtParams.Search["value"] : null;
                if (!string.IsNullOrEmpty(searchValue))
                    query = query.Where(s => s.NameAr.Contains(searchValue));
                int recordsTotal = await query.CountAsync();
                var data = await query.OrderBy(s => s.NameAr).Skip(dtParams.Start).Take(dtParams.Length)
                    .Select(s => new { s.Id, s.NameAr, s.Price, s.Unit, s.NormalRange, ResultType = (int)s.ResultType, s.PredefinedValues, s.Notes, s.IsActive })
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
                var test = new ClinicService
                {
                    NameAr = model.NameAr,
                    NameEn = model.NameEn,
                    Type = ServiceType.LabTest, // Force Type to LabTest
                    Price = model.Price,
                    Notes = model.Notes,
                    Unit = model.Unit,
                    NormalRange = model.NormalRange,
                    ResultType = model.ResultType,
                    PredefinedValues = model.PredefinedValues,
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

                test.NameAr = model.NameAr;
                test.Price = model.Price;
                test.Unit = model.Unit;
                test.NormalRange = model.NormalRange;
                test.ResultType = model.ResultType;
                test.PredefinedValues = model.PredefinedValues;
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
                Type = test.Type,
                Price = test.Price,
                Notes = test.Notes,
                Unit = test.Unit,
                NormalRange = test.NormalRange,
                IsActive = test.IsActive
            };

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

                    test.NameAr = model.NameAr;
                    test.NameEn = model.NameEn;
                    test.Price = model.Price;
                    test.Notes = model.Notes;
                    test.Unit = model.Unit;
                    test.NormalRange = model.NormalRange;
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
            return View(model);
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
    }
}
