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

        public async Task<IActionResult> PrintThermalReceipt(int id)
        {
            var sale = await _salesService.GetSaleDetailsAsync(id);
            if (sale == null) return NotFound();
            return View(sale);
        }

        [HttpGet]
        public async Task<IActionResult> LookupByBarcode(string barcode)
        {
            var safeBarcode = (barcode ?? "").Trim();
            if (string.IsNullOrEmpty(safeBarcode)) return BadRequest();

            // ── Step 1: Strict Barcode Isolation ──
            // Query ONLY batches that exactly match the physical barcode scanned.
            // Never substitute a different barcode just because they share the same parent item.
            var allMatchingBatches = await _context.ItemBatches
                .Include(b => b.Item)
                    .ThenInclude(i => i!.Unit)
                .Include(b => b.Item)
                    .ThenInclude(i => i!.Location)
                .Where(b => b.Barcode == safeBarcode && b.Item != null && b.Item.IsActive)
                .OrderBy(b => b.ExpiryDate)
                .ToListAsync();

            // Also check if barcode exists on the master PharmacyItems table
            if (!allMatchingBatches.Any())
            {
                var masterItem = await _context.PharmacyItems
                    .FirstOrDefaultAsync(i => i.Barcode == safeBarcode && i.IsActive);

                if (masterItem == null)
                {
                    // The barcode is truly unknown to the system
                    return Json(new { success = false, status = "NOT_FOUND", message = "الصنف غير معرف في النظام" });
                }

                // Item exists in catalog but has zero batch records with this barcode
                return Json(new {
                    success = false,
                    status = "OUT_OF_STOCK",
                    message = $"عفواً، رصيد الباتش ({safeBarcode}) من الصنف ({masterItem.Name}) صفر في المخزن."
                });
            }

            // ── Step 2: Filter to sellable batches only (Qty > 0, not expired) ──
            var sellableBatches = allMatchingBatches
                .Where(b => b.QuantityRemaining > 0 && b.ExpiryDate.Date >= DateTime.Today)
                .ToList();

            if (!sellableBatches.Any())
            {
                // Batches exist for this exact barcode, but all are empty or expired
                var firstFound = allMatchingBatches.First();
                var batchNum = firstFound.BatchNumber ?? safeBarcode;
                var itemName = firstFound.Item?.Name ?? "الصنف";

                return Json(new {
                    success = false,
                    status = "OUT_OF_STOCK",
                    message = $"عفواً، رصيد الباتش ({batchNum}) من الصنف ({itemName}) صفر في المخزن."
                });
            }

            // ── Step 3: Build response DTOs ──
            var results = sellableBatches.Select(b => new BarcodeLookupResult
            {
                ItemId = b.Item!.Id,
                ItemName = b.Item.Name,
                Barcode = b.Barcode,
                BatchNumber = b.BatchNumber,
                UnitName = b.Item.Unit?.Name ?? "-",
                SlotCode = b.Item.Location?.Code ?? "-",
                DefaultSellingPrice = b.SellingPrice > 0 ? b.SellingPrice : b.Item.DefaultSellingPrice,
                AvailableQuantity = b.QuantityRemaining,
                ExpiryDate = b.ExpiryDate,
                ExpiryDateFormatted = b.ExpiryDate.ToString("yyyy-MM-dd"),
                BatchId = b.Id
            }).ToList();

            // ── Step 4: Route based on batch count ──
            // 1 batch  → auto-populate the row
            // N batches → force the pharmacist to pick via modal
            if (results.Count == 1)
            {
                return Json(new { success = true, multiMatch = false, data = results[0] });
            }

            return Json(new { success = true, multiMatch = true, matches = results });
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
