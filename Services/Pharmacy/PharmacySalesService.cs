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
                query = query.Where(s => (s.CustomerName != null && s.CustomerName.Contains(searchValue)) || s.Id.ToString().Contains(searchValue));
            }

            int recordsTotal = await query.CountAsync();

            // Dynamic Sorting
            if (dtParams.Order != null && dtParams.Order.Any())
            {
                var order = dtParams.Order.First();
                var direction = order.Dir.ToLower() == "asc" ? "asc" : "desc";
                
                switch (order.Column)
                {
                    case 0:
                        query = direction == "asc" ? query.OrderBy(s => s.Id) : query.OrderByDescending(s => s.Id);
                        break;
                    case 1:
                        query = direction == "asc" ? query.OrderBy(s => s.SaleDate) : query.OrderByDescending(s => s.SaleDate);
                        break;
                    case 2:
                        query = direction == "asc" ? query.OrderBy(s => s.CustomerName) : query.OrderByDescending(s => s.CustomerName);
                        break;
                    case 3:
                        query = direction == "asc" ? query.OrderBy(s => s.Lines.Count) : query.OrderByDescending(s => s.Lines.Count);
                        break;
                    case 4:
                        query = direction == "asc" ? query.OrderBy(s => s.Status) : query.OrderByDescending(s => s.Status);
                        break;
                    case 5:
                        query = direction == "asc" ? query.OrderBy(s => s.TotalAmount) : query.OrderByDescending(s => s.TotalAmount);
                        break;
                    default:
                        query = query.OrderByDescending(s => s.SaleDate);
                        break;
                }
            }
            else
            {
                query = query.OrderByDescending(s => s.SaleDate);
            }

            var data = await query
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
                .Where(l => l.ItemId > 0 && l.Quantity > 0 && l.SellingPrice > 0)
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
                ItemBatchId = l.ItemBatchId,
                Barcode = l.Barcode?.Trim(),
                Quantity = l.Quantity,
                SellingPrice = l.SellingPrice
            }).ToList();

            return await _stockService.ExecuteSaleAsync(sale, lineRequests);
        }

        public async Task<SaleExecutionResult> EditSaleAsync(PharmacySaleEditViewModel model)
        {
            try
            {
                var strategy = _context.Database.CreateExecutionStrategy();
                return await strategy.ExecuteAsync(async () =>
                {
                    await using var transaction = await _context.Database.BeginTransactionAsync();
                    try
                    {
                        // Step A: Snapshot
                        var sale = await _context.PharmacySales
                            .Include(s => s.Lines)
                            .ThenInclude(l => l.ItemBatch)
                            .FirstOrDefaultAsync(s => s.Id == model.SaleId);

                        if (sale == null)
                            return new SaleExecutionResult { Success = false, Message = "الفاتورة غير موجودة" };

                        sale.CustomerName = model.CustomerName?.Trim();
                        sale.Notes = model.Notes?.Trim();

                        // Step B: Restore unconditionally
                        foreach (var oldLine in sale.Lines)
                        {
                            if (oldLine.ItemBatch != null)
                            {
                                oldLine.ItemBatch.QuantityRemaining += oldLine.Quantity;
                                oldLine.ItemBatch.UpdatedAt = DateTime.Now;
                            }
                        }

                        // Step C: Apply New
                        _context.PharmacySaleLines.RemoveRange(sale.Lines);
                        sale.Lines.Clear();
                        await _context.SaveChangesAsync(); // Ensure items are removed before FEFO logic runs again

                        var newTotal = 0m;
                        var validIncoming = model.Lines.Where(l => l.ItemId > 0 && l.Quantity > 0 && l.SellingPrice > 0).ToList();

                        foreach (var incoming in validIncoming)
                        {
                            // Allocate stock via FEFO
                            var allocation = await _stockService.PreviewFefoAllocationAsync(
                                incoming.ItemId, incoming.Quantity, DateTime.Today);

                            if (!allocation.Success)
                            {
                                // Custom validation exception handling as requested
                                await transaction.RollbackAsync();
                                throw new InvalidOperationException($"الكمية المطلوبة غير متوفرة في المخزون للصنف رقم {incoming.ItemId}. {allocation.Message}");
                            }

                            foreach (var alloc in allocation.Allocations)
                            {
                                var batch = await _context.ItemBatches
                                    .Include(b => b.Item).ThenInclude(i => i!.Location)
                                    .FirstOrDefaultAsync(b => b.Id == alloc.BatchId);

                                if (batch == null || batch.QuantityRemaining < alloc.Quantity)
                                {
                                    await transaction.RollbackAsync();
                                    throw new InvalidOperationException("الكمية غير كافية في المخزن");
                                }

                                batch.QuantityRemaining -= alloc.Quantity;
                                batch.UpdatedAt = DateTime.Now;

                                var saleLine = new PharmacySaleLine
                                {
                                    SaleId = sale.Id,
                                    ItemId = incoming.ItemId,
                                    ItemBatchId = batch.Id,
                                    Quantity = alloc.Quantity,
                                    UnitPrice = incoming.SellingPrice,
                                    LineTotal = alloc.Quantity * incoming.SellingPrice,
                                    BatchNumberSnapshot = batch.BatchNumber,
                                    ExpiryDateSnapshot = batch.ExpiryDate.Date,
                                    SlotCodeSnapshot = batch.Item?.Location?.Code ?? "-",
                                    CreatedAt = DateTime.Now
                                };
                                _context.PharmacySaleLines.Add(saleLine);
                                newTotal += saleLine.LineTotal;
                            }
                        }

                        // Step D: Finalize
                        sale.TotalAmount = newTotal;

                        // Assign RowVersion for concurrency check
                        _context.Entry(sale).Property("RowVersion").OriginalValue = model.RowVersion;

                        try
                        {
                            await _context.SaveChangesAsync();
                            await transaction.CommitAsync();
                        }
                        catch (DbUpdateConcurrencyException)
                        {
                            await transaction.RollbackAsync();
                            throw new NagmClinic.Exceptions.ConcurrencyException("هذا السجل تم تعديله بواسطة مستخدم آخر. يرجى تحديث الصفحة والمحاولة مرة أخرى.");
                        }

                        return new SaleExecutionResult
                        {
                            Success = true,
                            Message = "تم تعديل الفاتورة بنجاح",
                            SaleId = sale.Id
                        };
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                });
            }
            catch (NagmClinic.Exceptions.ConcurrencyException)
            {
                throw;
            }
            catch (InvalidOperationException ex)
            {
                return new SaleExecutionResult { Success = false, Message = ex.Message };
            }
            catch
            {
                throw;
            }
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
