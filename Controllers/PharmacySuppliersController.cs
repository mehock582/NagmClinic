using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NagmClinic.Data;
using NagmClinic.Models;
using NagmClinic.ViewModels;
using NagmClinic.Extensions;
using NagmClinic.Models.DataTables;

namespace NagmClinic.Controllers
{
    public class PharmacySuppliersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PharmacySuppliersController(ApplicationDbContext context)
        {
            _context = context;
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
                var query = _context.PharmacySuppliers.AsQueryable();
                var searchValue = dtParams.Search != null && dtParams.Search.ContainsKey("value") ? dtParams.Search["value"] : null;
                if (!string.IsNullOrEmpty(searchValue))
                    query = query.Where(s => s.Name.Contains(searchValue) || (s.PhoneNumber != null && s.PhoneNumber.Contains(searchValue)));
                int recordsTotal = await query.CountAsync();
                var data = await query.OrderBy(s => s.Name).Skip(dtParams.Start).Take(dtParams.Length)
                    .Select(s => new { s.Id, s.Name, s.ContactPerson, s.PhoneNumber, s.Address, s.Notes, s.IsActive })
                    .ToListAsync();
                return Json(new DataTablesResponse<object> { draw = dtParams.Draw, recordsTotal = recordsTotal, recordsFiltered = recordsTotal, data = data });
            }
            catch (Exception ex) { return Json(new DataTablesResponse<object> { error = ex.Message }); }
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
