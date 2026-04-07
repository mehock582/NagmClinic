using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NagmClinic.Data;
using NagmClinic.Models;
using NagmClinic.ViewModels;
using NagmClinic.Extensions;
using NagmClinic.Models.DataTables;

namespace NagmClinic.Controllers
{
    public class PharmacyCategoriesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PharmacyCategoriesController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            ViewData["Title"] = "تصنيفات الصيدلية";
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> GetCategoriesData()
        {
            try
            {
                var dtParams = Request.GetDataTablesParameters();
                var query = _context.PharmacyCategories.AsQueryable();
                var searchValue = dtParams.Search != null && dtParams.Search.ContainsKey("value") ? dtParams.Search["value"] : null;
                if (!string.IsNullOrEmpty(searchValue))
                    query = query.Where(c => c.Name.Contains(searchValue));
                int recordsTotal = await query.CountAsync();
                var data = await query.OrderBy(c => c.Name).Skip(dtParams.Start).Take(dtParams.Length)
                    .Select(c => new { c.Id, c.Name, c.Description, c.IsActive })
                    .ToListAsync();
                return Json(new DataTablesResponse<object> { draw = dtParams.Draw, recordsTotal = recordsTotal, recordsFiltered = recordsTotal, data = data });
            }
            catch (Exception ex) { return Json(new DataTablesResponse<object> { error = ex.Message }); }
        }

        [HttpPost]
        public async Task<IActionResult> QuickCreate([FromBody] PharmacyCategoryViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "تحقق من البيانات المدخلة" });
            }

            var name = model.Name.Trim();
            var exists = await _context.PharmacyCategories.AnyAsync(c => c.Name == name);
            if (exists)
            {
                return Json(new { success = false, message = "التصنيف موجود مسبقاً" });
            }

            var category = new PharmacyCategory
            {
                Name = name,
                Description = model.Description?.Trim(),
                IsActive = true
            };

            _context.PharmacyCategories.Add(category);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> QuickEdit([FromBody] PharmacyCategoryViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "تحقق من البيانات المدخلة" });
            }

            var category = await _context.PharmacyCategories.FindAsync(model.Id);
            if (category == null)
            {
                return Json(new { success = false, message = "التصنيف غير موجود" });
            }

            var name = model.Name.Trim();
            var duplicate = await _context.PharmacyCategories.AnyAsync(c => c.Id != model.Id && c.Name == name);
            if (duplicate)
            {
                return Json(new { success = false, message = "اسم التصنيف مستخدم بالفعل" });
            }

            category.Name = name;
            category.Description = model.Description?.Trim();
            category.IsActive = model.IsActive;

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var category = await _context.PharmacyCategories.FindAsync(id);
            if (category == null)
            {
                return Json(new { success = false, message = "التصنيف غير موجود" });
            }

            category.IsActive = !category.IsActive;
            await _context.SaveChangesAsync();
            return Json(new { success = true, isActive = category.IsActive });
        }
    }
}
