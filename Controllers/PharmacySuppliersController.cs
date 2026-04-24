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
    public class PharmacySuppliersController : BaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly IPharmacyMasterDataService _masterDataService;

        public PharmacySuppliersController(ApplicationDbContext context, IPharmacyMasterDataService masterDataService)
        {
            _context = context;
            _masterDataService = masterDataService;
        }

        public IActionResult Index()
        {
            ViewData["Title"] = "موردي الصيدلية";
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> GetSuppliersData()
        {
            try
            {
                var dtParams = Request.GetDataTablesParameters();
                var response = await _masterDataService.GetSuppliersDataAsync(dtParams);
                return Json(response);
            }
            catch (Exception ex)
            {
                return Json(new DataTablesResponse<object> { error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> QuickCreate([FromBody] PharmacySupplierViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "تحقق من البيانات المدخلة" });
            }

            var name = model.Name.Trim();
            var exists = await _context.PharmacySuppliers.AnyAsync(s => s.Name == name);
            if (exists)
            {
                return Json(new { success = false, message = "اسم المورد موجود مسبقاً" });
            }

            var supplier = new PharmacySupplier
            {
                Name = name,
                ContactPerson = model.ContactPerson?.Trim(),
                PhoneNumber = model.PhoneNumber?.Trim(),
                Address = model.Address?.Trim(),
                Notes = model.Notes?.Trim(),
                IsActive = true
            };

            _context.PharmacySuppliers.Add(supplier);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> QuickEdit([FromBody] PharmacySupplierViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "تحقق من البيانات المدخلة" });
            }

            var supplier = await _context.PharmacySuppliers.FindAsync(model.Id);
            if (supplier == null)
            {
                return Json(new { success = false, message = "المورد غير موجود" });
            }

            var name = model.Name.Trim();
            var duplicate = await _context.PharmacySuppliers.AnyAsync(s => s.Id != model.Id && s.Name == name);
            if (duplicate)
            {
                return Json(new { success = false, message = "اسم المورد مستخدم بالفعل" });
            }

            supplier.Name = name;
            supplier.ContactPerson = model.ContactPerson?.Trim();
            supplier.PhoneNumber = model.PhoneNumber?.Trim();
            supplier.Address = model.Address?.Trim();
            supplier.Notes = model.Notes?.Trim();
            supplier.IsActive = model.IsActive;

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var supplier = await _context.PharmacySuppliers.FindAsync(id);
            if (supplier == null)
            {
                return Json(new { success = false, message = "المورد غير موجود" });
            }

            supplier.IsActive = !supplier.IsActive;
            await _context.SaveChangesAsync();
            return Json(new { success = true, isActive = supplier.IsActive });
        }
    }
}
