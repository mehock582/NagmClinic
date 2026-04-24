using Microsoft.EntityFrameworkCore;
using NagmClinic.Data;
using NagmClinic.Models;
using NagmClinic.Models.Enums;

namespace NagmClinic.Services.Pharmacy
{
    public class PharmacyStockService : IPharmacyStockService
    {
        private readonly ApplicationDbContext _context;

        public PharmacyStockService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<string> GenerateUniqueBarcodeAsync(CancellationToken cancellationToken = default)
        {
            while (true)
            {
                var candidate = $"{DateTime.UtcNow:yyMMddHHmmss}{Random.Shared.Next(100, 999)}";
                var exists = await _context.ItemBatches.AnyAsync(b => b.Barcode == candidate, cancellationToken);
                if (!exists)
                {
                    return candidate;
                }
            }
        }

        public async Task<BarcodeLookupResult?> LookupByBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
        {
            var normalized = barcode?.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return null;
            }

            // 1. First, try to find an exact batch match (specific manufacturer barcode or internal barcode)
            var batch = await _context.ItemBatches
                .Include(b => b.Item)
                    .ThenInclude(i => i!.Unit)
                .Include(b => b.Item)
                    .ThenInclude(i => i!.Location)
                .Where(b => b.Barcode == normalized && (b.Item == null || b.Item.IsActive) && b.QuantityRemaining > 0 && b.ExpiryDate >= DateTime.Today)
                .OrderBy(b => b.ExpiryDate)
                .FirstOrDefaultAsync(cancellationToken);

            // 2. If no matching batch is found by barcode, search for an Item with this barcode and pick its best batch
            if (batch == null)
            {
                var itemWithBarcode = await _context.PharmacyItems
                    .Include(i => i.Unit)
                    .Include(i => i.Location)
                    .Include(i => i.Batches)
                    .FirstOrDefaultAsync(i => i.Barcode == normalized && i.IsActive);

                if (itemWithBarcode != null)
                {
                    batch = itemWithBarcode.Batches
                        .Where(b => b.QuantityRemaining > 0 && b.ExpiryDate >= DateTime.Today)
                        .OrderBy(b => b.ExpiryDate)
                        .FirstOrDefault();
                }
            }

            if (batch == null || batch.Item == null)
            {
                return null;
            }

            var item = batch.Item;
            var available = await GetAvailableStockAsync(item.Id, DateTime.Today, cancellationToken);

            return new BarcodeLookupResult
            {
                ItemId = item.Id,
                ItemName = item.Name,
                Barcode = batch.Barcode,
                BatchNumber = batch.BatchNumber,
                UnitName = item.Unit?.Name ?? "-",
                SlotCode = item.Location?.Code ?? "-",
                DefaultSellingPrice = batch.SellingPrice > 0 ? batch.SellingPrice : item.DefaultSellingPrice,
                AvailableQuantity = available,
                ExpiryDate = batch.ExpiryDate,
                ExpiryDateFormatted = batch.ExpiryDate.ToString("yyyy-MM-dd"),
                BatchId = batch.Id,
                SuggestedBatch = new FefoAllocationLine
                {
                    BatchId = batch.Id,
                    BatchNumber = batch.BatchNumber,
                    Barcode = batch.Barcode,
                    ExpiryDate = batch.ExpiryDate,
                    Quantity = batch.QuantityRemaining,
                    Available = batch.QuantityRemaining,
                    Remaining = batch.QuantityRemaining, // Placeholder or same for single lookups
                    UnitPrice = batch.SellingPrice > 0 ? batch.SellingPrice : item.DefaultSellingPrice,
                    SlotCode = item.Location?.Code ?? "-"
                }
            };
        }

        public async Task<List<BarcodeLookupResult>> LookupAllByBarcodeAsync(string barcode, CancellationToken cancellationToken = default)
        {
            var normalized = barcode?.Trim();
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return new List<BarcodeLookupResult>();
            }

            var today = DateTime.Today;

            var batches = await _context.ItemBatches
                .Include(b => b.Item)
                    .ThenInclude(i => i!.Unit)
                .Include(b => b.Item)
                    .ThenInclude(i => i!.Location)
                .Where(b => b.Barcode == normalized
                         && b.QuantityRemaining > 0
                         && b.ExpiryDate.Date >= today
                         && b.Item != null
                         && b.Item.IsActive)
                .OrderBy(b => b.ExpiryDate)
                .ToListAsync(cancellationToken);

            var results = new List<BarcodeLookupResult>();

            foreach (var batch in batches)
            {
                var item = batch.Item!;
                // Correct Fix: Use the specific batch quantity, not the aggregate item quantity
                var batchStock = batch.QuantityRemaining;

                results.Add(new BarcodeLookupResult
                {
                    ItemId = item.Id,
                    ItemName = item.Name,
                    Barcode = batch.Barcode,
                    BatchNumber = batch.BatchNumber,
                    UnitName = item.Unit?.Name ?? "-",
                    SlotCode = item.Location?.Code ?? "-",
                    DefaultSellingPrice = batch.SellingPrice > 0 ? batch.SellingPrice : item.DefaultSellingPrice,
                    AvailableQuantity = batchStock,
                    ExpiryDate = batch.ExpiryDate,
                    ExpiryDateFormatted = batch.ExpiryDate.ToString("yyyy-MM-dd"),
                    BatchId = batch.Id,
                    SuggestedBatch = new FefoAllocationLine
                    {
                        BatchId = batch.Id,
                        BatchNumber = batch.BatchNumber,
                        Barcode = batch.Barcode,
                        ExpiryDate = batch.ExpiryDate,
                        Quantity = batch.QuantityRemaining,
                        Available = batch.QuantityRemaining,
                        Remaining = batch.QuantityRemaining,
                        UnitPrice = batch.SellingPrice > 0 ? batch.SellingPrice : item.DefaultSellingPrice,
                        SlotCode = item.Location?.Code ?? "-"
                    }
                });
            }

            return results;
        }

        public async Task<decimal> GetAvailableStockAsync(int itemId, DateTime? asOfDate = null, CancellationToken cancellationToken = default)
        {
            var date = (asOfDate ?? DateTime.Today).Date;

            return await _context.ItemBatches
                .Where(b => b.ItemId == itemId && b.ExpiryDate.Date >= date && b.QuantityRemaining > 0)
                .SumAsync(b => (decimal?)b.QuantityRemaining, cancellationToken) ?? 0m;
        }

        public async Task<FefoAllocationResult> PreviewFefoAllocationAsync(int itemId, decimal requestedQuantity, DateTime? asOfDate = null, CancellationToken cancellationToken = default)
        {
            var date = (asOfDate ?? DateTime.Today).Date;
            if (requestedQuantity <= 0)
            {
                return new FefoAllocationResult
                {
                    Success = false,
                    Message = "الكمية المطلوبة غير صحيحة"
                };
            }

            var item = await _context.PharmacyItems
                .Include(i => i.Location)
                .FirstOrDefaultAsync(i => i.Id == itemId, cancellationToken);

            if (item == null)
            {
                return new FefoAllocationResult
                {
                    Success = false,
                    Message = "الصنف غير موجود"
                };
            }

            var candidateBatches = await _context.ItemBatches
                .Where(b => b.ItemId == itemId && b.QuantityRemaining > 0 && b.ExpiryDate.Date >= date)
                .OrderBy(b => b.ExpiryDate)
                .ThenBy(b => b.CreatedAt)
                .ToListAsync(cancellationToken);

            var available = candidateBatches.Sum(b => b.QuantityRemaining);
            if (available < requestedQuantity)
            {
                return new FefoAllocationResult
                {
                    Success = false,
                    Message = $"المتاح للصنف {available:N2} فقط",
                    AvailableQuantity = available
                };
            }

            var remaining = requestedQuantity;
            var allocations = new List<FefoAllocationLine>();

            foreach (var batch in candidateBatches)
            {
                if (remaining <= 0)
                {
                    break;
                }

                var taken = Math.Min(remaining, batch.QuantityRemaining);
                if (taken <= 0)
                {
                    continue;
                }

                allocations.Add(new FefoAllocationLine
                {
                    BatchId = batch.Id,
                    BatchNumber = batch.BatchNumber,
                    Barcode = batch.Barcode,
                    ExpiryDate = batch.ExpiryDate,
                    Quantity = taken,
                    Available = batch.QuantityRemaining,
                    Remaining = batch.QuantityRemaining - taken,
                    UnitPrice = batch.SellingPrice > 0 ? batch.SellingPrice : item.DefaultSellingPrice,
                    SlotCode = item.Location?.Code ?? "-"
                });

                remaining -= taken;
            }

            return new FefoAllocationResult
            {
                Success = remaining <= 0,
                Message = remaining <= 0 ? "تم تطبيق FEFO بنجاح" : "تعذر تخصيص الكمية بالكامل",
                AvailableQuantity = available,
                Allocations = allocations
            };
        }

        public async Task<PurchaseExecutionResult> ExecutePurchaseAsync(
            PharmacyPurchase purchase,
            IReadOnlyCollection<PharmacyPurchaseRequestLine> requestedLines,
            CancellationToken cancellationToken = default)
        {
            if (requestedLines == null || requestedLines.Count == 0)
            {
                return new PurchaseExecutionResult { Success = false, Message = "يجب إضافة بند واحد على الأقل" };
            }

            var today = DateTime.Today;
            var cleanedLines = requestedLines
                .Where(l => l.ItemId > 0 && !string.IsNullOrWhiteSpace(l.BatchNumber) && !string.IsNullOrWhiteSpace(l.Barcode))
                .ToList();

            if (cleanedLines.Count == 0)
            {
                return new PurchaseExecutionResult { Success = false, Message = "بيانات البنود (باتش وباركود) غير مكتملة" };
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    purchase.PurchaseDate = purchase.PurchaseDate == default ? DateTime.Now : purchase.PurchaseDate;
                    _context.PharmacyPurchases.Add(purchase);
                    await _context.SaveChangesAsync(cancellationToken);

                    decimal total = 0m;

                    foreach (var line in cleanedLines)
                    {
                        if (line.ExpiryDate.Date < today)
                        {
                            await transaction.RollbackAsync(cancellationToken);
                            return new PurchaseExecutionResult
                            {
                                Success = false,
                                Message = $"لا يمكن شراء باتش منتهي للصنف رقم {line.ItemId}"
                            };
                        }

                        if (line.Quantity <= 0 || line.PurchasePrice < 0)
                        {
                            await transaction.RollbackAsync(cancellationToken);
                            return new PurchaseExecutionResult
                            {
                                Success = false,
                                Message = $"بيانات الكمية أو الأسعار غير صحيحة للصنف رقم {line.ItemId}"
                            };
                        }

                        var item = await _context.PharmacyItems
                            .FirstOrDefaultAsync(i => i.Id == line.ItemId, cancellationToken);

                        if (item == null)
                        {
                            await transaction.RollbackAsync(cancellationToken);
                            return new PurchaseExecutionResult { Success = false, Message = $"الصنف رقم {line.ItemId} غير موجود" };
                        }

                        // Strategic Fix: Link the scanned barcode to the item itself if not already set.
                        // This allows any future batch to find this item by manufacturer barcode.
                        if (string.IsNullOrWhiteSpace(item.Barcode))
                        {
                            item.Barcode = line.Barcode.Trim();
                        }

                        var batch = await _context.ItemBatches
                            .FirstOrDefaultAsync(
                                b => b.ItemId == line.ItemId && b.BatchNumber == line.BatchNumber.Trim() && b.Barcode == line.Barcode.Trim(),
                                cancellationToken);

                        var effectiveSellingPrice = batch != null
                            ? (batch.SellingPrice > 0 ? batch.SellingPrice : item.DefaultSellingPrice)
                            : item.DefaultSellingPrice;

                        var purchaseLine = new PharmacyPurchaseLine
                        {
                            PurchaseId = purchase.Id,
                            ItemId = line.ItemId,
                            BatchNumber = line.BatchNumber.Trim(),
                            Barcode = line.Barcode.Trim(),
                            ExpiryDate = line.ExpiryDate.Date,
                            Quantity = line.Quantity,
                            BonusQuantity = 0,
                            PurchasePrice = line.PurchasePrice,
                            LineTotal = line.Quantity * line.PurchasePrice
                        };

                        _context.PharmacyPurchaseLines.Add(purchaseLine);
                        await _context.SaveChangesAsync(cancellationToken);

                        var receivedTotal = purchaseLine.Quantity;

                        if (batch == null)
                        {
                            batch = new ItemBatch
                            {
                                ItemId = line.ItemId,
                                BatchNumber = purchaseLine.BatchNumber,
                                Barcode = purchaseLine.Barcode,
                                ExpiryDate = purchaseLine.ExpiryDate.Date,
                                QuantityReceived = purchaseLine.Quantity,
                                BonusQuantity = 0,
                                QuantityRemaining = receivedTotal,
                                PurchasePrice = purchaseLine.PurchasePrice,
                                SellingPrice = effectiveSellingPrice,
                                SupplierId = purchase.SupplierId
                            };
                            _context.ItemBatches.Add(batch);
                            await _context.SaveChangesAsync(cancellationToken);
                        }
                        else
                        {
                            batch.ExpiryDate = purchaseLine.ExpiryDate.Date;
                            batch.QuantityReceived += purchaseLine.Quantity;
                            batch.BonusQuantity = 0;
                            batch.QuantityRemaining += receivedTotal;
                            batch.PurchasePrice = purchaseLine.PurchasePrice;
                            if (batch.SellingPrice <= 0 && effectiveSellingPrice > 0)
                            {
                                batch.SellingPrice = effectiveSellingPrice;
                            }
                            batch.SupplierId = purchase.SupplierId;
                        }

                        purchaseLine.ItemBatchId = batch.Id;
                        total += purchaseLine.LineTotal;
                    }

                    purchase.TotalAmount = total;
                    await _context.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);

                    return new PurchaseExecutionResult
                    {
                        Success = true,
                        Message = "تم حفظ فاتورة الشراء بنجاح",
                        PurchaseId = purchase.Id
                    };
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }
            });
        }

        public async Task<SaleExecutionResult> ExecuteSaleAsync(
            PharmacySale sale,
            IReadOnlyCollection<PharmacySaleRequestLine> requestedLines,
            CancellationToken cancellationToken = default)
        {
            if (requestedLines == null || requestedLines.Count == 0)
            {
                return new SaleExecutionResult { Success = false, Message = "يجب إضافة بند واحد على الأقل" };
            }

            var lines = requestedLines.Where(l => l.ItemId > 0 && l.Quantity > 0 && l.SellingPrice > 0).ToList();
            if (lines.Count == 0)
            {
                return new SaleExecutionResult { Success = false, Message = "بيانات البنود غير صحيحة" };
            }

            var strategy = _context.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    sale.SaleDate = sale.SaleDate == default ? DateTime.Now : sale.SaleDate;
                    sale.Status = PharmacySaleStatus.Completed;
                    _context.PharmacySales.Add(sale);
                    await _context.SaveChangesAsync(cancellationToken);

                    decimal total = 0m;

                    foreach (var line in lines)
                    {
                        List<FefoAllocationLine> allocationsToProcess = new List<FefoAllocationLine>();

                        if (line.ItemBatchId.HasValue && line.ItemBatchId.Value > 0)
                        {
                            // Manual Batch Selection (from Modal)
                            var batch = await _context.ItemBatches
                                .Include(b => b.Item)
                                    .ThenInclude(i => i!.Location)
                                .FirstOrDefaultAsync(b => b.Id == line.ItemBatchId.Value, cancellationToken);

                            if (batch == null || batch.ItemId != line.ItemId || batch.QuantityRemaining < line.Quantity || batch.ExpiryDate.Date < DateTime.Today)
                            {
                                await transaction.RollbackAsync(cancellationToken);
                                return new SaleExecutionResult { Success = false, Message = $"الباتش المحدد غير متاح أو كميته غير كافية للصنف رقم {line.ItemId}" };
                            }

                            allocationsToProcess.Add(new FefoAllocationLine
                            {
                                BatchId = batch.Id,
                                BatchNumber = batch.BatchNumber,
                                Barcode = batch.Barcode,
                                ExpiryDate = batch.ExpiryDate,
                                Quantity = line.Quantity,
                                Available = batch.QuantityRemaining,
                                Remaining = batch.QuantityRemaining - line.Quantity,
                                UnitPrice = batch.SellingPrice,
                                SlotCode = batch.Item?.Location?.Code ?? "-"
                            });
                        }
                        else
                        {
                            // Automatic FEFO Allocation
                            var allocation = await PreviewFefoAllocationAsync(line.ItemId, line.Quantity, DateTime.Today, cancellationToken);
                            if (!allocation.Success)
                            {
                                await transaction.RollbackAsync(cancellationToken);
                                return new SaleExecutionResult
                                {
                                    Success = false,
                                    Message = $"تعذر صرف الصنف رقم {line.ItemId}: {allocation.Message}"
                                };
                            }
                            allocationsToProcess.AddRange(allocation.Allocations);
                        }

                        foreach (var alloc in allocationsToProcess)
                        {
                            var batch = await _context.ItemBatches
                                .Include(b => b.Item)
                                    .ThenInclude(i => i!.Location)
                                .FirstOrDefaultAsync(b => b.Id == alloc.BatchId, cancellationToken);

                            if (batch == null)
                            {
                                await transaction.RollbackAsync(cancellationToken);
                                return new SaleExecutionResult { Success = false, Message = "باتش غير موجود أثناء عملية الصرف" };
                            }

                            if (batch.ExpiryDate.Date < DateTime.Today)
                            {
                                await transaction.RollbackAsync(cancellationToken);
                                return new SaleExecutionResult { Success = false, Message = $"الباتش {batch.BatchNumber} منتهي الصلاحية" };
                            }

                            if (batch.QuantityRemaining < alloc.Quantity)
                            {
                                await transaction.RollbackAsync(cancellationToken);
                                return new SaleExecutionResult { Success = false, Message = $"نفدت الكمية من الباتش {batch.BatchNumber}" };
                            }

                            batch.QuantityRemaining -= alloc.Quantity;
                            batch.UpdatedAt = DateTime.Now;

                            var unitPrice = line.SellingPrice > 0 ? line.SellingPrice : alloc.UnitPrice;

                            var saleLine = new PharmacySaleLine
                            {
                                SaleId = sale.Id,
                                ItemId = line.ItemId,
                                ItemBatchId = batch.Id,
                                Quantity = alloc.Quantity,
                                UnitPrice = unitPrice,
                                LineTotal = alloc.Quantity * unitPrice,
                                BatchNumberSnapshot = batch.BatchNumber,
                                ExpiryDateSnapshot = batch.ExpiryDate.Date,
                                SlotCodeSnapshot = batch.Item?.Location?.Code ?? "-",
                                CreatedAt = DateTime.Now
                            };

                            _context.PharmacySaleLines.Add(saleLine);
                            total += saleLine.LineTotal;
                        }
                    }

                    sale.TotalAmount = total;
                    await _context.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);

                    return new SaleExecutionResult
                    {
                        Success = true,
                        Message = "تم صرف الفاتورة بنجاح",
                        SaleId = sale.Id
                    };
                }
                catch
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw;
                }
            });
        }

        public async Task<NagmClinic.Models.DataTables.DataTablesResponse<object>> GetItemsDataAsync(NagmClinic.Models.DataTables.DataTablesParameters dtParams)
        {
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

            return new NagmClinic.Models.DataTables.DataTablesResponse<object> { draw = dtParams.Draw, recordsTotal = recordsTotal, recordsFiltered = recordsTotal, data = data };
        }

        public async Task<NagmClinic.Models.DataTables.DataTablesResponse<object>> GetInventoryItemsDataAsync(NagmClinic.Models.DataTables.DataTablesParameters dtParams)
        {
            var today = DateTime.Today;
            var query = _context.PharmacyItems
                .Include(i => i.Unit).Include(i => i.Category).Include(i => i.Location).AsQueryable();

            var searchValue = dtParams.Search != null && dtParams.Search.ContainsKey("value") ? dtParams.Search["value"] : null;
            if (!string.IsNullOrEmpty(searchValue))
            {
                query = query.Where(i => i.Name.Contains(searchValue) || 
                                       (i.Category != null && i.Category.Name.Contains(searchValue)));
            }

            int recordsTotal = await query.CountAsync();
            var dataItems = await query.OrderBy(i => i.Name)
                .Skip(dtParams.Start).Take(dtParams.Length)
                .Select(i => new NagmClinic.ViewModels.InventoryItemSummaryViewModel
                {
                    Name = i.Name,
                    UnitName = i.Unit != null ? i.Unit.Name : "-",
                    CategoryName = i.Category != null ? i.Category.Name : "-",
                    Location = i.Location != null ? i.Location.Code : "-",
                    ReorderLevel = i.ReorderLevel,
                    AvailableQuantity = i.Batches.Where(b => b.QuantityRemaining > 0 && b.ExpiryDate.Date >= today).Sum(b => (decimal?)b.QuantityRemaining) ?? 0
                }).ToListAsync();

            var data = dataItems.Cast<object>().ToList();
            return new NagmClinic.Models.DataTables.DataTablesResponse<object> { draw = dtParams.Draw, recordsTotal = recordsTotal, recordsFiltered = recordsTotal, data = data };
        }

        public async Task<NagmClinic.Models.DataTables.DataTablesResponse<object>> GetInventoryBatchesDataAsync(NagmClinic.Models.DataTables.DataTablesParameters dtParams)
        {
            var today = DateTime.Today;
            var query = _context.ItemBatches.Include(b => b.Item).Include(b => b.Supplier).AsQueryable();

            var searchValue = dtParams.Search != null && dtParams.Search.ContainsKey("value") ? dtParams.Search["value"] : null;
            if (!string.IsNullOrEmpty(searchValue))
            {
                query = query.Where(b => (b.Item != null && b.Item.Name.Contains(searchValue)) || 
                                       b.BatchNumber.Contains(searchValue) || 
                                       b.Barcode.Contains(searchValue));
            }

            int recordsTotal = await query.CountAsync();
            var dataBatches = await query.OrderBy(b => b.ExpiryDate).ThenBy(b => b.ItemId)
                .Skip(dtParams.Start).Take(dtParams.Length)
                .Select(b => new NagmClinic.ViewModels.InventoryBatchDetailViewModel
                {
                    ItemName = b.Item != null ? b.Item.Name : "-",
                    BatchNumber = b.BatchNumber,
                    Barcode = b.Barcode,
                    ExpiryDate = b.ExpiryDate,
                    Quantity = b.QuantityRemaining,
                    PurchasePrice = b.PurchasePrice,
                    Supplier = b.Supplier != null ? b.Supplier.Name : "-",
                    Status = b.ExpiryDate.Date < today ? "Expired" : 
                             (b.ExpiryDate.Date <= today.AddDays(60) ? "Near Expiry" : "OK")
                }).ToListAsync();

            var data = dataBatches.Cast<object>().ToList();
            return new NagmClinic.Models.DataTables.DataTablesResponse<object> { draw = dtParams.Draw, recordsTotal = recordsTotal, recordsFiltered = recordsTotal, data = data };
        }
    }
}
