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
    public class PharmacySalesController : Controller
    {
        private readonly IPharmacySalesService _salesService;
        private readonly IPharmacyStockService _stockService;
        private readonly ApplicationDbContext _context; // Still needed for some simple lookups not yet moved

        public PharmacySalesController(IPharmacySalesService salesService, IPharmacyStockService stockService, ApplicationDbContext context)
        {
            _salesService = salesService;
            _stockService = stockService;
            _context = context;
        }

        public IActionResult Index()
        {
            ViewData["Title"] = "صرف الأدوية";
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> GetSalesData()
        {
            try
            {
                var dtParams = Request.GetDataTablesParameters();
                var response = await _salesService.GetSalesDataAsync(dtParams);
                return Json(response);
            }
            catch (Exception ex)
            {
                return Json(new DataTablesResponse<object> { error = ex.Message });
            }
        }

        public async Task<IActionResult> Create()
        {
            ViewData["Title"] = "إضافة فاتورة صرف";
            var model = await _salesService.BuildCreateViewModelAsync();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PharmacySaleCreateViewModel model)
        {
            ViewData["Title"] = "إضافة فاتورة صرف";

            if (!ModelState.IsValid)
            {
                // We still need to reload lookups on failure
                // I'll add a helper to the service for this
                await ReloadLookupsAsync(model);
                return View(model);
            }

            var result = await _salesService.ExecuteSaleAsync(model);
            if (!result.Success)
            {
                ModelState.AddModelError(string.Empty, result.Message);
                await ReloadLookupsAsync(model);
                return View(model);
            }

            TempData["SuccessMessage"] = result.Message;
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Details(int id)
        {
            ViewData["Title"] = "تفاصيل فاتورة الصرف";
            var sale = await _salesService.GetSaleDetailsAsync(id);
            if (sale == null) return NotFound();
            return View(sale);
        }

        [HttpGet]
        public async Task<IActionResult> LookupByBarcode(string barcode)
        {
            var result = await _stockService.LookupByBarcodeAsync(barcode);
            if (result == null)
            {
                return Json(new { success = false, message = "لم يتم العثور على الباركود" });
            }

            return Json(new { success = true, data = result });
        }

        [HttpGet]
        public async Task<IActionResult> PreviewFefo(int itemId, decimal quantity)
        {
            var result = await _stockService.PreviewFefoAllocationAsync(itemId, quantity, DateTime.Today);
            return Json(new
            {
                success = result.Success,
                message = result.Message,
                available = result.AvailableQuantity,
                allocations = result.Allocations
            });
        }

        [HttpGet]
        public async Task<IActionResult> SearchItems(string query)
        {
            query = (query ?? string.Empty).Trim();

            var items = await _context.PharmacyItems
                .Include(i => i.Unit)
                .Include(i => i.Location)
                .Where(i => i.IsActive && (query == "" || i.Name.Contains(query)))
                .OrderBy(i => i.Name)
                .Take(20)
                .Select(i => new
                {
                    i.Id,
                    i.Name,
                    UnitName = i.Unit != null ? i.Unit.Name : "-",
                    SlotCode = i.Location != null ? i.Location.Code : "-"
                })
                .ToListAsync();

            return Json(items);
        }

        private async Task ReloadLookupsAsync(PharmacySaleCreateViewModel model)
        {
            // Simple bridge to the service's internal lookup logic if we want to keep controller lean
            // For now, I'll just re-call the service's build method logic or add a dedicated method
             var freshModel = await _salesService.BuildCreateViewModelAsync();
             model.ItemLookup = freshModel.ItemLookup;
        }
    }
}
