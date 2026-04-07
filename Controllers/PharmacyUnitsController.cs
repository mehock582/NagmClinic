using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NagmClinic.Data;
using NagmClinic.Models;
using NagmClinic.ViewModels;
using NagmClinic.Extensions;
using NagmClinic.Models.DataTables;

namespace NagmClinic.Controllers
{
    public class PharmacyUnitsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PharmacyUnitsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            ViewData["Title"] = "وحدات الصيدلية";
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> GetUnitsData()
        {
            try
            {
                var dtParams = Request.GetDataTablesParameters();
                var query = _context.PharmacyUnits.AsQueryable();
                var searchValue = dtParams.Search != null && dtParams.Search.ContainsKey("value") ? dtParams.Search["value"] : null;
                if (!string.IsNullOrEmpty(searchValue))
                    query = query.Where(u => u.Name.Contains(searchValue));
                int recordsTotal = await query.CountAsync();
                var data = await query.OrderBy(u => u.Name).Skip(dtParams.Start).Take(dtParams.Length)
                    .Select(u => new { u.Id, u.Name, u.NameEn, u.IsActive })
                    .ToListAsync();
                return Json(new DataTablesResponse<object> { draw = dtParams.Draw, recordsTotal = recordsTotal, recordsFiltered = recordsTotal, data = data });
            }
            catch (Exception ex) { return Json(new DataTablesResponse<object> { error = ex.Message }); }
        }

        [HttpPost]
        public async Task<IActionResult> QuickCreate([FromBody] PharmacyUnitViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "تحقق من البيانات المدخلة" });
            }

            var name = model.Name.Trim();
            var exists = await _context.PharmacyUnits.AnyAsync(u => u.Name == name);
            if (exists)
            {
                return Json(new { success = false, message = "اسم الوحدة موجود مسبقاً" });
            }

            var unit = new PharmacyUnit
            {
                Name = name,
                NameEn = model.NameEn?.Trim(),
                IsActive = true
            };

            _context.PharmacyUnits.Add(unit);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> QuickEdit([FromBody] PharmacyUnitViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "تحقق من البيانات المدخلة" });
            }

            var unit = await _context.PharmacyUnits.FindAsync(model.Id);
            if (unit == null)
            {
                return Json(new { success = false, message = "الوحدة غير موجودة" });
            }

            var name = model.Name.Trim();
            var duplicate = await _context.PharmacyUnits.AnyAsync(u => u.Id != model.Id && u.Name == name);
            if (duplicate)
            {
                return Json(new { success = false, message = "اسم الوحدة مستخدم بالفعل" });
            }

            unit.Name = name;
            unit.NameEn = model.NameEn?.Trim();
            unit.IsActive = model.IsActive;

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var unit = await _context.PharmacyUnits.FindAsync(id);
            if (unit == null)
            {
                return Json(new { success = false, message = "الوحدة غير موجودة" });
            }

            unit.IsActive = !unit.IsActive;
            await _context.SaveChangesAsync();
            return Json(new { success = true, isActive = unit.IsActive });
        }
    }
}
