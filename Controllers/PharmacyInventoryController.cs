using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NagmClinic.Data;
using NagmClinic.ViewModels;
using NagmClinic.Extensions;
using NagmClinic.Models.DataTables;

namespace NagmClinic.Controllers
{
    public class PharmacyInventoryController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PharmacyInventoryController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            ViewData["Title"] = "مخزون الصيدلية";
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> GetInventoryItemsData()
        {
            try
            {
                var dtParams = Request.GetDataTablesParameters();
                var today = DateTime.Today;
                var query = _context.PharmacyItems
                    .Include(i => i.Unit).Include(i => i.Category).Include(i => i.Location).AsQueryable();
                var searchValue = dtParams.Search != null && dtParams.Search.ContainsKey("value") ? dtParams.Search["value"] : null;
                if (!string.IsNullOrEmpty(searchValue))
                    query = query.Where(i => i.Name.Contains(searchValue));
                int recordsTotal = await query.CountAsync();
                var data = await query.OrderBy(i => i.Name).Skip(dtParams.Start).Take(dtParams.Length)
                    .Select(i => new
                    {
                        i.Name,
                        UnitName = i.Unit != null ? i.Unit.Name : "-",
                        CategoryName = i.Category != null ? i.Category.Name : "-",
                        SlotCode = i.Location != null ? i.Location.Code : "-",
                        i.ReorderLevel,
                        AvailableQuantity = i.Batches.Where(b => b.QuantityRemaining > 0 && b.ExpiryDate.Date >= today).Sum(b => (decimal?)b.QuantityRemaining) ?? 0
                    }).ToListAsync();
                return Json(new DataTablesResponse<object> { draw = dtParams.Draw, recordsTotal = recordsTotal, recordsFiltered = recordsTotal, data = data });
            }
            catch (Exception ex) { return Json(new DataTablesResponse<object> { error = ex.Message }); }
        }

        [HttpPost]
        public async Task<IActionResult> GetInventoryBatchesData()
        {
            try
            {
                var dtParams = Request.GetDataTablesParameters();
                var today = DateTime.Today;
                var query = _context.ItemBatches.Include(b => b.Item).Include(b => b.Supplier).AsQueryable();
                var searchValue = dtParams.Search != null && dtParams.Search.ContainsKey("value") ? dtParams.Search["value"] : null;
                if (!string.IsNullOrEmpty(searchValue))
                    query = query.Where(b => (b.Item != null && b.Item.Name.Contains(searchValue)) || b.BatchNumber.Contains(searchValue) || b.Barcode.Contains(searchValue));
                int recordsTotal = await query.CountAsync();
                var data = await query.OrderBy(b => b.ExpiryDate).ThenBy(b => b.ItemId).Skip(dtParams.Start).Take(dtParams.Length)
                    .Select(b => new
                    {
                        ItemName = b.Item != null ? b.Item.Name : "-",
                        b.BatchNumber,
                        b.Barcode,
                        ExpiryDate = b.ExpiryDate.ToString("yyyy-MM-dd"),
                        b.QuantityRemaining, b.PurchasePrice, b.SellingPrice,
                        SupplierName = b.Supplier != null ? b.Supplier.Name : "-",
                        IsExpired = b.ExpiryDate.Date < today,
                        NearExpiry = b.ExpiryDate.Date >= today && b.ExpiryDate.Date <= today.AddDays(60)
                    }).ToListAsync();
                return Json(new DataTablesResponse<object> { draw = dtParams.Draw, recordsTotal = recordsTotal, recordsFiltered = recordsTotal, data = data });
            }
            catch (Exception ex) { return Json(new DataTablesResponse<object> { error = ex.Message }); }
        }
    }
}
