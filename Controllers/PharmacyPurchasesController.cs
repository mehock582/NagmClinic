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
    public class PharmacyPurchasesController : Controller
    {
        private readonly IPharmacyPurchasesService _purchaseService;
        private readonly IPharmacyStockService _stockService;
        private readonly ApplicationDbContext _context;

        public PharmacyPurchasesController(IPharmacyPurchasesService purchaseService, IPharmacyStockService stockService, ApplicationDbContext context)
        {
            _purchaseService = purchaseService;
            _stockService = stockService;
            _context = context;
        }

        public IActionResult Index()
        {
            ViewData["Title"] = "مشتريات الصيدلية";
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> GetPurchasesData()
        {
            try
            {
                var dtParams = Request.GetDataTablesParameters();
                var response = await _purchaseService.GetPurchasesDataAsync(dtParams);
                return Json(response);
            }
            catch (Exception ex)
            {
                return Json(new DataTablesResponse<object> { error = ex.Message });
            }
        }

        public async Task<IActionResult> Create()
        {
            ViewData["Title"] = "إضافة فاتورة شراء";
            var model = await _purchaseService.BuildCreateViewModelAsync();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PharmacyPurchaseCreateViewModel model)
        {
            ViewData["Title"] = "إضافة فاتورة شراء";

            if (model.Lines == null || model.Lines.Count == 0)
            {
                ModelState.AddModelError(nameof(model.Lines), "يجب إضافة بند شراء واحد على الأقل");
            }

            if (!ModelState.IsValid)
            {
                await ReloadLookupsAsync(model);
                return View(model);
            }

            var result = await _purchaseService.ExecutePurchaseAsync(model);
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
            ViewData["Title"] = "تفاصيل فاتورة الشراء";
            var purchase = await _purchaseService.GetPurchaseDetailsAsync(id);
            if (purchase == null) return NotFound();
            return View(purchase);
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
        public async Task<IActionResult> GetNextBatchNumber()
        {
            try
            {
                var batchNumber = await _purchaseService.GenerateBatchNumberAsync();
                return Json(new { success = true, batchNumber });
            }
            catch (Exception)
            {
                return Json(new { success = false, message = "حدث خطأ أثناء توليد رقم الباتش" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetNextBarcode()
        {
            try
            {
                var barcode = await _purchaseService.GenerateBarcodeAsync();
                return Json(new { success = true, barcode });
            }
            catch (Exception)
            {
                return Json(new { success = false, message = "حدث خطأ أثناء توليد الباركود" });
            }
        }

        [HttpGet]
        public async Task<IActionResult> SearchItems(string query)
        {
            query = (query ?? string.Empty).Trim();

            var items = await _context.PharmacyItems
                .Include(i => i.Unit)
                .Where(i => i.IsActive && (query == "" || i.Name.Contains(query)))
                .OrderBy(i => i.Name)
                .Take(20)
                .Select(i => new
                {
                    i.Id,
                    i.Name,
                    UnitName = i.Unit != null ? i.Unit.Name : "-",
                    i.DefaultSellingPrice
                })
                .ToListAsync();

            return Json(items);
        }

        private async Task ReloadLookupsAsync(PharmacyPurchaseCreateViewModel model)
        {
            var freshModel = await _purchaseService.BuildCreateViewModelAsync();
            model.SupplierLookup = freshModel.SupplierLookup;
            model.ItemLookup = freshModel.ItemLookup;
        }
    }
}

