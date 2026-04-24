using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NagmClinic.Data;
using NagmClinic.Models;
using NagmClinic.ViewModels;
using NagmClinic.Extensions;
using NagmClinic.Models.DataTables;
using NagmClinic.Services.Pharmacy;

namespace NagmClinic.Controllers
{
    [Authorize(Roles = "Admin")]
    public class PharmacyCategoriesController : BaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly IPharmacyMasterDataService _masterDataService;

        public PharmacyCategoriesController(ApplicationDbContext context, IPharmacyMasterDataService masterDataService)
        {
            _context = context;
            _masterDataService = masterDataService;
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
                var response = await _masterDataService.GetCategoriesDataAsync(dtParams);
                return Json(response);
            }
            catch (Exception ex)
            {
                return Json(new DataTablesResponse<object> { error = ex.Message });
            }
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
