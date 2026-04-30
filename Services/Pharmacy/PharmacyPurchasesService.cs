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

            // Dynamic Sorting
            if (dtParams.Order != null && dtParams.Order.Any())
            {
                var order = dtParams.Order.First();
                var direction = order.Dir.ToLower() == "asc" ? "asc" : "desc";

                switch (order.Column)
                {
                    case 0:
                        query = direction == "asc" ? query.OrderBy(p => p.InvoiceNumber).ThenBy(p => p.Id) : query.OrderByDescending(p => p.InvoiceNumber).ThenByDescending(p => p.Id);
                        break;
                    case 1:
                        query = direction == "asc" ? query.OrderBy(p => p.PurchaseDate) : query.OrderByDescending(p => p.PurchaseDate);
                        break;
                    case 2:
                        query = direction == "asc" ? query.OrderBy(p => p.Supplier!.Name) : query.OrderByDescending(p => p.Supplier!.Name);
                        break;
                    case 3:
                        query = direction == "asc" ? query.OrderBy(p => p.Lines.Count) : query.OrderByDescending(p => p.Lines.Count);
                        break;
                    case 4:
                        query = direction == "asc" ? query.OrderBy(p => p.TotalAmount) : query.OrderByDescending(p => p.TotalAmount);
                        break;
                    default:
                        query = query.OrderByDescending(p => p.PurchaseDate);
                        break;
                }
            }
            else
            {
                query = query.OrderByDescending(p => p.PurchaseDate);
            }
            
            var data = await query.Skip(dtParams.Start).Take(dtParams.Length)
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
                .Where(l => l.ItemId > 0 && !string.IsNullOrWhiteSpace(l.Barcode) && l.Quantity > 0)
                .ToList();

            if (validLines.Count == 0)
            {
                return new PurchaseExecutionResult { Success = false, Message = "يجب إضافة بند شراء واحد على الأقل مع الباركود" };
            }

            var purchase = new PharmacyPurchase
            {
                SupplierId = model.SupplierId,
                InvoiceNumber = model.InvoiceNumber?.Trim(),
                PurchaseDate = model.PurchaseDate,
                Notes = model.Notes?.Trim()
            };

            var requestLines = new List<PharmacyPurchaseRequestLine>();
            foreach(var l in validLines)
            {
                requestLines.Add(new PharmacyPurchaseRequestLine
                {
                    ItemId = l.ItemId,
                    BatchNumber = await GenerateBatchNumberAsync(),
                    Barcode = l.Barcode.Trim(),
                    ExpiryDate = l.ExpiryDate.Date,
                    Quantity = l.Quantity,
                    PurchasePrice = l.PurchasePrice
                });
            }

            return await _stockService.ExecutePurchaseAsync(purchase, requestLines);
        }

        public async Task<string> GenerateBatchNumberAsync()
        {
            string datePart = DateTime.Today.ToString("yyMMdd");
            int attempt = 1;
            while (attempt < 10000)
            {
                string candidate = $"{datePart}{attempt:D3}";
                bool exists = await _context.ItemBatches.AnyAsync(b => b.BatchNumber == candidate);
                if (!exists) return candidate;
                attempt++;
            }
            // fallback if extremely high volume today
            return $"{datePart}{new Random().Next(10000, 99999)}";
        }

        public async Task<string> GenerateBarcodeAsync()
        {
            return await _stockService.GenerateUniqueBarcodeAsync();
        }

        public async Task<PurchaseExecutionResult> EditPurchaseAsync(PharmacyPurchaseEditViewModel model)
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
                        var purchase = await _context.PharmacyPurchases
                            .Include(p => p.Lines)
                            .ThenInclude(l => l.ItemBatch)
                            .FirstOrDefaultAsync(p => p.Id == model.PurchaseId);

                        if (purchase == null)
                            return new PurchaseExecutionResult { Success = false, Message = "فاتورة الشراء غير موجودة" };

                        purchase.SupplierId = model.SupplierId;
                        purchase.InvoiceNumber = model.InvoiceNumber?.Trim();
                        purchase.Notes = model.Notes?.Trim();

                        // Step B: Restore Phase (Unconditionally subtract old quantities)
                        foreach (var oldLine in purchase.Lines)
                        {
                            if (oldLine.ItemBatch != null)
                            {
                                oldLine.ItemBatch.QuantityReceived -= oldLine.Quantity;
                                oldLine.ItemBatch.QuantityRemaining -= oldLine.Quantity;

                                if (oldLine.ItemBatch.QuantityRemaining < 0)
                                {
                                    await transaction.RollbackAsync();
                                    throw new InvalidOperationException($"لا يمكن تقليل الكمية المشتراة للصنف رقم {oldLine.ItemId} لأن جزءاً منها تم بيعه بالفعل.");
                                }

                                oldLine.ItemBatch.UpdatedAt = DateTime.Now;
                            }
                        }

                        // Step C: Apply New Phase
                        _context.PharmacyPurchaseLines.RemoveRange(purchase.Lines);
                        purchase.Lines.Clear();
                        await _context.SaveChangesAsync();

                        decimal newTotal = 0m;
                        var validIncoming = model.Lines.Where(l => l.ItemId > 0 && l.Quantity > 0).ToList();

                        foreach (var incoming in validIncoming)
                        {
                            var item = await _context.PharmacyItems.FirstOrDefaultAsync(i => i.Id == incoming.ItemId);
                            if (item == null)
                            {
                                await transaction.RollbackAsync();
                                throw new InvalidOperationException($"الصنف رقم {incoming.ItemId} غير موجود");
                            }

                            // Try to generate or reuse batch
                            var batchNumber = await GenerateBatchNumberAsync();

                            var newPurchaseLine = new PharmacyPurchaseLine
                            {
                                PurchaseId = purchase.Id,
                                ItemId = incoming.ItemId,
                                BatchNumber = batchNumber,
                                Barcode = incoming.Barcode.Trim(),
                                ExpiryDate = incoming.ExpiryDate.Date,
                                Quantity = incoming.Quantity,
                                BonusQuantity = 0,
                                PurchasePrice = incoming.PurchasePrice,
                                SellingPrice = item.DefaultSellingPrice,
                                LineTotal = incoming.Quantity * incoming.PurchasePrice,
                                CreatedAt = DateTime.Now
                            };

                            _context.PharmacyPurchaseLines.Add(newPurchaseLine);
                            await _context.SaveChangesAsync();

                            var existingBatch = await _context.ItemBatches.FirstOrDefaultAsync(b =>
                                b.ItemId == incoming.ItemId &&
                                b.Barcode == incoming.Barcode.Trim());

                            if (existingBatch == null)
                            {
                                var newBatch = new ItemBatch
                                {
                                    ItemId = incoming.ItemId,
                                    BatchNumber = batchNumber,
                                    Barcode = incoming.Barcode.Trim(),
                                    ExpiryDate = incoming.ExpiryDate.Date,
                                    QuantityReceived = incoming.Quantity,
                                    BonusQuantity = 0,
                                    QuantityRemaining = incoming.Quantity,
                                    PurchasePrice = incoming.PurchasePrice,
                                    SellingPrice = item.DefaultSellingPrice,
                                    SupplierId = purchase.SupplierId
                                };
                                _context.ItemBatches.Add(newBatch);
                                await _context.SaveChangesAsync();
                                newPurchaseLine.ItemBatchId = newBatch.Id;
                                newPurchaseLine.BatchNumber = batchNumber;
                            }
                            else
                            {
                                existingBatch.QuantityReceived += incoming.Quantity;
                                existingBatch.QuantityRemaining += incoming.Quantity;
                                existingBatch.PurchasePrice = incoming.PurchasePrice; // Update cost price if changed
                                existingBatch.ExpiryDate = incoming.ExpiryDate.Date;
                                newPurchaseLine.ItemBatchId = existingBatch.Id;
                                newPurchaseLine.BatchNumber = existingBatch.BatchNumber; // Reuse existing batch number
                            }

                            newTotal += newPurchaseLine.LineTotal;
                        }

                        // Step D: Finalize
                        purchase.TotalAmount = newTotal;

                        // Assign RowVersion for concurrency check
                        _context.Entry(purchase).Property("RowVersion").OriginalValue = model.RowVersion;

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

                        return new PurchaseExecutionResult
                        {
                            Success = true,
                            Message = "تم تعديل فاتورة الشراء بنجاح",
                            PurchaseId = purchase.Id
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
                return new PurchaseExecutionResult { Success = false, Message = ex.Message };
            }
            catch
            {
                throw;
            }
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
                    UnitName = i.Unit != null ? i.Unit.Name : "-"
                })
                .ToListAsync();
        }
    }
}
