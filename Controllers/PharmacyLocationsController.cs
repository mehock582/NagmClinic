using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NagmClinic.Data;
using NagmClinic.Models;
using NagmClinic.ViewModels;
using NagmClinic.Extensions;
using NagmClinic.Models.DataTables;

namespace NagmClinic.Controllers
{
    public class PharmacyLocationsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PharmacyLocationsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            ViewData["Title"] = "مواقع التخزين";
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> GetLocationsData()
        {
            try
            {
                var dtParams = Request.GetDataTablesParameters();
                var query = _context.PharmacyLocations.AsQueryable();
                var searchValue = dtParams.Search != null && dtParams.Search.ContainsKey("value") ? dtParams.Search["value"] : null;
                if (!string.IsNullOrEmpty(searchValue))
                    query = query.Where(l => l.Code.Contains(searchValue) || l.Description.Contains(searchValue));
                int recordsTotal = await query.CountAsync();
                var data = await query.OrderBy(l => l.Code).Skip(dtParams.Start).Take(dtParams.Length)
                    .Select(l => new { l.Id, l.Code, l.Description, l.IsActive })
                    .ToListAsync();
                return Json(new DataTablesResponse<object> { draw = dtParams.Draw, recordsTotal = recordsTotal, recordsFiltered = recordsTotal, data = data });
            }
            catch (Exception ex) { return Json(new DataTablesResponse<object> { error = ex.Message }); }
        }

        [HttpPost]
        public async Task<IActionResult> QuickCreate([FromBody] PharmacyLocationViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "تحقق من البيانات المدخلة" });
            }

            var code = model.Code.Trim().ToUpperInvariant();
            var exists = await _context.PharmacyLocations.AnyAsync(l => l.Code == code);
            if (exists)
            {
                return Json(new { success = false, message = "كود الموقع موجود مسبقاً" });
            }

            var location = new PharmacyLocation
            {
                Code = code,
                Description = model.Description.Trim(),
                IsActive = true
            };

            _context.PharmacyLocations.Add(location);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> QuickEdit([FromBody] PharmacyLocationViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "تحقق من البيانات المدخلة" });
            }

            var location = await _context.PharmacyLocations.FindAsync(model.Id);
            if (location == null)
            {
                return Json(new { success = false, message = "الموقع غير موجود" });
            }

            var code = model.Code.Trim().ToUpperInvariant();
            var duplicate = await _context.PharmacyLocations.AnyAsync(l => l.Id != model.Id && l.Code == code);
            if (duplicate)
            {
                return Json(new { success = false, message = "كود الموقع مستخدم بالفعل" });
            }

            location.Code = code;
            location.Description = model.Description.Trim();
            location.IsActive = model.IsActive;

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var location = await _context.PharmacyLocations.FindAsync(id);
            if (location == null)
            {
                return Json(new { success = false, message = "الموقع غير موجود" });
            }

            location.IsActive = !location.IsActive;
            await _context.SaveChangesAsync();
            return Json(new { success = true, isActive = location.IsActive });
        }
    }
}
