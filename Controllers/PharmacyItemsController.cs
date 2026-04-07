using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NagmClinic.Data;
using NagmClinic.Models;
using NagmClinic.Services.Pharmacy;
using NagmClinic.ViewModels;
using NagmClinic.Extensions;
using NagmClinic.Models.DataTables;

namespace NagmClinic.Controllers
{
    public class PharmacyItemsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IPharmacyStockService _stockService;

        public PharmacyItemsController(ApplicationDbContext context, IPharmacyStockService stockService)
        {
            _context = context;
            _stockService = stockService;
        }

        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = "أصناف الصيدلية";
            await LoadMasterLookupsAsync();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> GetItemsData()
        {
            try
            {
                var dtParams = Request.GetDataTablesParameters();
                var today = DateTime.Today;
                var query = _context.PharmacyItems
                    .Include(i => i.Unit)
                    .Include(i => i.Category)
                    .Include(i => i.Location)
                    .AsQueryable();

                var searchValue = dtParams.Search != null && dtParams.Search.ContainsKey("value") ? dtParams.Search["value"] : null;
                if (!string.IsNullOrEmpty(searchValue))
                    query = query.Where(i => i.Name.Contains(searchValue) || (i.GenericName != null && i.GenericName.Contains(searchValue)));

                int recordsTotal = await query.CountAsync();
                var data = await query.OrderBy(i => i.Name).Skip(dtParams.Start).Take(dtParams.Length)
                    .Select(i => new
                    {
                        i.Id, i.Name, i.GenericName,
                        i.UnitId, UnitName = i.Unit != null ? i.Unit.Name : "-",
                        i.CategoryId, CategoryName = i.Category != null ? i.Category.Name : "-",
                        i.LocationId, LocationCode = i.Location != null ? i.Location.Code : "-",
                        i.DefaultSellingPrice, i.ReorderLevel, i.IsActive,
                        AvailableStock = i.Batches
                            .Where(b => b.ExpiryDate.Date >= today && b.QuantityRemaining > 0)
                            .Sum(b => (decimal?)b.QuantityRemaining) ?? 0
                    }).ToListAsync();

                return Json(new DataTablesResponse<object> { draw = dtParams.Draw, recordsTotal = recordsTotal, recordsFiltered = recordsTotal, data = data });
            }
            catch (Exception ex) { return Json(new DataTablesResponse<object> { error = ex.Message }); }
        }

        [HttpPost]
        public async Task<IActionResult> QuickCreate([FromBody] PharmacyItemViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "تحقق من البيانات المدخلة" });
            }

            if (!await ValidateReferencesAsync(model.UnitId, model.CategoryId, model.LocationId))
            {
                return Json(new { success = false, message = "الوحدة أو التصنيف أو الموقع غير صحيح" });
            }

            var item = new PharmacyItem
            {
                Name = model.Name.Trim(),
                GenericName = model.GenericName?.Trim(),
                UnitId = model.UnitId,
                CategoryId = model.CategoryId,
                LocationId = model.LocationId,
                DefaultSellingPrice = model.DefaultSellingPrice,
                ReorderLevel = model.ReorderLevel,
                IsActive = true
            };

            _context.PharmacyItems.Add(item);
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> QuickEdit([FromBody] PharmacyItemViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return Json(new { success = false, message = "تحقق من البيانات المدخلة" });
            }

            var item = await _context.PharmacyItems.FindAsync(model.Id);
            if (item == null)
            {
                return Json(new { success = false, message = "الصنف غير موجود" });
            }

            if (!await ValidateReferencesAsync(model.UnitId, model.CategoryId, model.LocationId))
            {
                return Json(new { success = false, message = "الوحدة أو التصنيف أو الموقع غير صحيح" });
            }

            item.Name = model.Name.Trim();
            item.GenericName = model.GenericName?.Trim();
            item.UnitId = model.UnitId;
            item.CategoryId = model.CategoryId;
            item.LocationId = model.LocationId;
            item.DefaultSellingPrice = model.DefaultSellingPrice;
            item.ReorderLevel = model.ReorderLevel;
            item.IsActive = model.IsActive;

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var item = await _context.PharmacyItems.FindAsync(id);
            if (item == null)
            {
                return Json(new { success = false, message = "الصنف غير موجود" });
            }

            item.IsActive = !item.IsActive;
            await _context.SaveChangesAsync();
            return Json(new { success = true, isActive = item.IsActive });
        }

        [HttpGet]
        public async Task<IActionResult> Search(string query)
        {
            query = (query ?? string.Empty).Trim();

            var data = await _context.PharmacyItems
                .Include(i => i.Unit)
                .Include(i => i.Location)
                .Where(i => i.IsActive && (query == "" || i.Name.Contains(query)))
                .OrderBy(i => i.Name)
                .Take(20)
                .Select(i => new
                {
                    i.Id,
                    i.Name,
                    Unit = i.Unit != null ? i.Unit.Name : "-",
                    Slot = i.Location != null ? i.Location.Code : "-"
                })
                .ToListAsync();

            return Json(data);
        }

        private async Task LoadMasterLookupsAsync()
        {
            ViewData["Units"] = await _context.PharmacyUnits
                .Where(u => u.IsActive)
                .OrderBy(u => u.Name)
                .Select(u => new { u.Id, u.Name })
                .ToListAsync();

            ViewData["Categories"] = await _context.PharmacyCategories
                .Where(c => c.IsActive)
                .OrderBy(c => c.Name)
                .Select(c => new { c.Id, c.Name })
                .ToListAsync();

            ViewData["Locations"] = await _context.PharmacyLocations
                .Where(l => l.IsActive)
                .OrderBy(l => l.Code)
                .Select(l => new { l.Id, l.Code, l.Description })
                .ToListAsync();
        }

        private async Task<bool> ValidateReferencesAsync(int unitId, int categoryId, int locationId)
        {
            return await _context.PharmacyUnits.AnyAsync(u => u.Id == unitId && u.IsActive)
                   && await _context.PharmacyCategories.AnyAsync(c => c.Id == categoryId && c.IsActive)
                   && await _context.PharmacyLocations.AnyAsync(l => l.Id == locationId && l.IsActive);
        }
    }
}
