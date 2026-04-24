using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NagmClinic.Data;
using NagmClinic.ViewModels;
using NagmClinic.Extensions;
using NagmClinic.Models.DataTables;

namespace NagmClinic.Controllers
{
    [Authorize(Roles = "Pharmacist,Admin")]
    public class PharmacyInventoryController : BaseController
    {
        private readonly ApplicationDbContext _context;
        private readonly NagmClinic.Services.Pharmacy.IPharmacyStockService _stockService;

        public PharmacyInventoryController(ApplicationDbContext context, NagmClinic.Services.Pharmacy.IPharmacyStockService stockService)
        {
            _context = context;
            _stockService = stockService;
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
                var response = await _stockService.GetInventoryItemsDataAsync(dtParams);
                return Json(response);
            }
            catch (Exception ex)
            {
                return Json(new DataTablesResponse<object> { error = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> GetInventoryBatchesData()
        {
            try
            {
                var dtParams = Request.GetDataTablesParameters();
                var response = await _stockService.GetInventoryBatchesDataAsync(dtParams);
                return Json(response);
            }
            catch (Exception ex)
            {
                return Json(new DataTablesResponse<object> { error = ex.Message });
            }
        }
    }
}
