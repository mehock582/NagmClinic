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
    public class PharmacySalesService : IPharmacySalesService
    {
        private readonly ApplicationDbContext _context;
        private readonly IPharmacyStockService _stockService;

        public PharmacySalesService(ApplicationDbContext context, IPharmacyStockService stockService)
        {
            _context = context;
            _stockService = stockService;
        }

        public async Task<DataTablesResponse<object>> GetSalesDataAsync(DataTablesParameters dtParams)
        {
            var query = _context.PharmacySales.Include(s => s.Lines).AsQueryable();

            var searchValue = dtParams.Search != null && dtParams.Search.ContainsKey("value") ? dtParams.Search["value"] : null;

            if (!string.IsNullOrEmpty(searchValue))
            {
                query = query.Where(s => s.CustomerName.Contains(searchValue) || s.Id.ToString().Contains(searchValue));
            }

            int recordsTotal = await query.CountAsync();

            var data = await query
                .OrderByDescending(s => s.SaleDate)
                .Skip(dtParams.Start)
                .Take(dtParams.Length)
                .Select(s => new
                {
                    Id = s.Id,
                    SaleDate = s.SaleDate.ToString("yyyy-MM-dd HH:mm"),
                    CustomerName = string.IsNullOrWhiteSpace(s.CustomerName) ? "-" : s.CustomerName,
                    Status = s.Status.ToString(),
                    TotalAmount = s.TotalAmount,
                    LinesCount = s.Lines.Count
                })
                .ToListAsync();

            return new DataTablesResponse<object>
            {
                draw = dtParams.Draw,
                recordsTotal = recordsTotal,
                recordsFiltered = recordsTotal,
                data = data
            };
        }

        public async Task<PharmacySale?> GetSaleDetailsAsync(int id)
        {
            return await _context.PharmacySales
                .Include(s => s.Lines)
                    .ThenInclude(l => l.Item)
                        .ThenInclude(i => i!.Unit)
                .Include(s => s.Lines)
                    .ThenInclude(l => l.ItemBatch)
                .FirstOrDefaultAsync(s => s.Id == id);
        }

        public async Task<PharmacySaleCreateViewModel> BuildCreateViewModelAsync()
        {
            var model = new PharmacySaleCreateViewModel();
            await LoadItemLookupAsync(model);
            model.Lines.Add(new PharmacySaleLineInputViewModel { Quantity = 1 });
            return model;
        }

        public async Task<SaleExecutionResult> ExecuteSaleAsync(PharmacySaleCreateViewModel model)
        {
            var validLines = model.Lines
                .Where(l => l.ItemId > 0 && l.Quantity > 0)
                .ToList();

            if (validLines.Count == 0)
            {
                return new SaleExecutionResult { Success = false, Message = "يجب إضافة بند صرف واحد على الأقل" };
            }

            var sale = new PharmacySale
            {
                SaleDate = model.SaleDate,
                CustomerName = model.CustomerName?.Trim(),
                Notes = model.Notes?.Trim()
            };

            var lineRequests = validLines.Select(l => new PharmacySaleRequestLine
            {
                ItemId = l.ItemId,
                Quantity = l.Quantity
            }).ToList();

            return await _stockService.ExecuteSaleAsync(sale, lineRequests);
        }

        private async Task LoadItemLookupAsync(PharmacySaleCreateViewModel model)
        {
            var today = DateTime.Today;
            model.ItemLookup = await _context.PharmacyItems
                .Include(i => i.Unit)
                .Include(i => i.Location)
                .Where(i => i.IsActive)
                .OrderBy(i => i.Name)
                .Select(i => new
                {
                    i.Id,
                    i.Name,
                    UnitName = i.Unit != null ? i.Unit.Name : "-",
                    SlotCode = i.Location != null ? i.Location.Code : "-",
                    i.DefaultSellingPrice,
                    Available = i.Batches
                        .Where(b => b.ExpiryDate.Date >= today && b.QuantityRemaining > 0)
                        .Sum(b => (decimal?)b.QuantityRemaining) ?? 0
                })
                .ToListAsync();
        }
    }
}
