using NagmClinic.Models;

namespace NagmClinic.Services.Pharmacy
{
    public interface IPharmacyStockService
    {
        Task<string> GenerateUniqueBarcodeAsync(CancellationToken cancellationToken = default);

        Task<BarcodeLookupResult?> LookupByBarcodeAsync(string barcode, CancellationToken cancellationToken = default);

        Task<decimal> GetAvailableStockAsync(int itemId, DateTime? asOfDate = null, CancellationToken cancellationToken = default);

        Task<FefoAllocationResult> PreviewFefoAllocationAsync(int itemId, decimal requestedQuantity, DateTime? asOfDate = null, CancellationToken cancellationToken = default);

        Task<PurchaseExecutionResult> ExecutePurchaseAsync(
            PharmacyPurchase purchase,
            IReadOnlyCollection<PharmacyPurchaseRequestLine> requestedLines,
            CancellationToken cancellationToken = default);

        Task<SaleExecutionResult> ExecuteSaleAsync(
            PharmacySale sale,
            IReadOnlyCollection<PharmacySaleRequestLine> requestedLines,
            CancellationToken cancellationToken = default);
    }

    public sealed class BarcodeLookupResult
    {
        public int ItemId { get; set; }
        public string ItemName { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public string BatchNumber { get; set; } = string.Empty;
        public string UnitName { get; set; } = string.Empty;
        public string SlotCode { get; set; } = string.Empty;
        public decimal DefaultSellingPrice { get; set; }
        public decimal AvailableQuantity { get; set; }
        public FefoAllocationLine? SuggestedBatch { get; set; }
    }

    public sealed class FefoAllocationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public decimal AvailableQuantity { get; set; }
        public List<FefoAllocationLine> Allocations { get; set; } = new();
    }

    public sealed class FefoAllocationLine
    {
        public int BatchId { get; set; }
        public string BatchNumber { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public DateTime ExpiryDate { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public string SlotCode { get; set; } = string.Empty;
    }

    public sealed class PharmacyPurchaseRequestLine
    {
        public int ItemId { get; set; }
        public string BatchNumber { get; set; } = string.Empty;
        public string Barcode { get; set; } = string.Empty;
        public DateTime ExpiryDate { get; set; }
        public decimal Quantity { get; set; }
        public decimal PurchasePrice { get; set; }
        public decimal SellingPrice { get; set; }
    }

    public sealed class PharmacySaleRequestLine
    {
        public int ItemId { get; set; }
        public decimal Quantity { get; set; }
    }

    public sealed class PurchaseExecutionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int? PurchaseId { get; set; }
    }

    public sealed class SaleExecutionResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int? SaleId { get; set; }
    }
}
