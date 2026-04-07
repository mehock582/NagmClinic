using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NagmClinic.Data;
using NagmClinic.Models;
using NagmClinic.Models.DataTables;
using NagmClinic.ViewModels;

namespace NagmClinic.Services.Pharmacy
{
    public class PharmacyPurchasesService : IPharmacyPurchasesService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPharmacyStockService _stockService;

        public PharmacyPurchasesService(ApplicationDbContext context, IPharmacyStockService stockService)
        {
            _context = context;
            _stockService = stockService;
        }

        public async Task<DataTablesResponse<object>> GetPurchasesDataAsync(DataTablesParameters dtParams)
        {
            var query = _context.PharmacyPurchases.Include(p => p.Supplier).Include(p => p.Lines).AsQueryable();
            var searchValue = dtParams.Search != null && dtParams.Search.ContainsKey("value") ? dtParams.Search["value"] : null;
            
            if (!string.IsNullOrEmpty(searchValue))
                query = query.Where(p => (p.InvoiceNumber != null && p.InvoiceNumber.Contains(searchValue)) || (p.Supplier != null && p.Supplier.Name.Contains(searchValue)));
            
            int recordsTotal = await query.CountAsync();
            
            var data = await query.OrderByDescending(p => p.PurchaseDate).Skip(dtParams.Start).Take(dtParams.Length)
                .Select(p => new
                {
                    p.Id,
                    InvoiceNumber = string.IsNullOrWhiteSpace(p.InvoiceNumber) ? "PO-" + p.Id.ToString("D5") : p.InvoiceNumber,
                    PurchaseDate = p.PurchaseDate.ToString("yyyy-MM-dd HH:mm"),
                    SupplierName = p.Supplier != null ? p.Supplier.Name : "-",
                    p.TotalAmount,
                    LinesCount = p.Lines.Count
                }).ToListAsync();
            
            return new DataTablesResponse<object>
            {
                draw = dtParams.Draw,
                recordsTotal = recordsTotal,
                recordsFiltered = recordsTotal,
                data = data
            };
        }

        public async Task<PharmacyPurchase?> GetPurchaseDetailsAsync(int id)
        {
            return await _context.PharmacyPurchases
                .Include(p => p.Supplier)
                .Include(p => p.Lines)
                    .ThenInclude(l => l.Item)
                        .ThenInclude(i => i!.Unit)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<PharmacyPurchaseCreateViewModel> BuildCreateViewModelAsync()
        {
            var model = new PharmacyPurchaseCreateViewModel();
            await LoadLookupsAsync(model);
            return model;
        }

        public async Task<PurchaseExecutionResult> ExecutePurchaseAsync(PharmacyPurchaseCreateViewModel model)
        {
            var validLines = model.Lines
                .Where(l => l.ItemId > 0 && !string.IsNullOrWhiteSpace(l.BatchNumber) && !string.IsNullOrWhiteSpace(l.Barcode) && l.Quantity > 0)
                .ToList();

            if (validLines.Count == 0)
            {
                return new PurchaseExecutionResult { Success = false, Message = "يجب إضافة بند شراء واحد على الأقل مع رقم الباتش والباركود" };
            }

            var purchase = new PharmacyPurchase
            {
                SupplierId = model.SupplierId,
                InvoiceNumber = model.InvoiceNumber?.Trim(),
                PurchaseDate = model.PurchaseDate,
                Notes = model.Notes?.Trim()
            };

            var requestLines = validLines.Select(l => new PharmacyPurchaseRequestLine
            {
                ItemId = l.ItemId,
                BatchNumber = l.BatchNumber.Trim(),
                Barcode = l.Barcode.Trim(),
                ExpiryDate = l.ExpiryDate.Date,
                Quantity = l.Quantity,
                PurchasePrice = l.PurchasePrice,
                SellingPrice = l.SellingPrice
            }).ToList();

            return await _stockService.ExecutePurchaseAsync(purchase, requestLines);
        }

        public async Task<string> GenerateBatchNumberAsync()
        {
            string datePart = DateTime.Today.ToString("yyyyMMdd");
            int attempt = 1;
            while (attempt < 1000)
            {
                string candidate = $"BATCH-{datePart}-{attempt:D3}";
                bool exists = await _context.ItemBatches.AnyAsync(b => b.BatchNumber == candidate);
                if (!exists) return candidate;
                attempt++;
            }
            return $"BATCH-{datePart}-{Guid.NewGuid().ToString("N").Substring(0, 4).ToUpper()}";
        }

        public async Task<string> GenerateBarcodeAsync()
        {
            return await _stockService.GenerateUniqueBarcodeAsync();
        }

        private async Task LoadLookupsAsync(PharmacyPurchaseCreateViewModel model)
        {
            model.SupplierLookup = await _context.PharmacySuppliers
                .Where(s => s.IsActive)
                .OrderBy(s => s.Name)
                .Select(s => new { s.Id, s.Name })
                .ToListAsync();

            model.ItemLookup = await _context.PharmacyItems
                .Include(i => i.Unit)
                .Where(i => i.IsActive)
                .OrderBy(i => i.Name)
                .Select(i => new
                {
                    i.Id,
                    i.Name,
                    UnitName = i.Unit != null ? i.Unit.Name : "-",
                    i.DefaultSellingPrice
                })
                .ToListAsync();
        }
    }
}


