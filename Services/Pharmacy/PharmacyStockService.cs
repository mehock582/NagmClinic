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

            var batch = await _context.ItemBatches
                .Include(b => b.Item)
                    .ThenInclude(i => i!.Unit)
                .Include(b => b.Item)
                    .ThenInclude(i => i!.Location)
                .FirstOrDefaultAsync(b => b.Barcode == normalized && (b.Item == null || b.Item.IsActive), cancellationToken);

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
                SuggestedBatch = new FefoAllocationLine
                {
                    BatchId = batch.Id,
                    BatchNumber = batch.BatchNumber,
                    Barcode = batch.Barcode,
                    ExpiryDate = batch.ExpiryDate,
                    Quantity = batch.QuantityRemaining,
                    UnitPrice = batch.SellingPrice > 0 ? batch.SellingPrice : item.DefaultSellingPrice,
                    SlotCode = item.Location?.Code ?? "-"
                }
            };
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
                .ThenBy(b => b.ReceivedAt)
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

                    if (line.Quantity <= 0 || line.PurchasePrice < 0 || line.SellingPrice < 0)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return new PurchaseExecutionResult
                        {
                            Success = false,
                            Message = $"بيانات الكمية أو الأسعار غير صحيحة للصنف رقم {line.ItemId}"
                        };
                    }

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
                        SellingPrice = line.SellingPrice,
                        LineTotal = line.Quantity * line.PurchasePrice,
                        CreatedAt = DateTime.Now
                    };

                    _context.PharmacyPurchaseLines.Add(purchaseLine);
                    await _context.SaveChangesAsync(cancellationToken);

                    var batch = await _context.ItemBatches
                        .FirstOrDefaultAsync(
                            b => b.ItemId == line.ItemId && b.BatchNumber == purchaseLine.BatchNumber && b.Barcode == purchaseLine.Barcode,
                            cancellationToken);

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
                            SellingPrice = purchaseLine.SellingPrice,
                            SupplierId = purchase.SupplierId,
                            ReceivedAt = DateTime.Now,
                            UpdatedAt = DateTime.Now
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
                        batch.SellingPrice = purchaseLine.SellingPrice;
                        batch.SupplierId = purchase.SupplierId;
                        batch.UpdatedAt = DateTime.Now;
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

            var lines = requestedLines.Where(l => l.ItemId > 0 && l.Quantity > 0).ToList();
            if (lines.Count == 0)
            {
                return new SaleExecutionResult { Success = false, Message = "بيانات البنود غير صحيحة" };
            }

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

                    foreach (var alloc in allocation.Allocations)
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

                        var saleLine = new PharmacySaleLine
                        {
                            SaleId = sale.Id,
                            ItemId = line.ItemId,
                            ItemBatchId = batch.Id,
                            Quantity = alloc.Quantity,
                            UnitPrice = alloc.UnitPrice,
                            LineTotal = alloc.Quantity * alloc.UnitPrice,
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
        }
    }
}
