using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NagmClinic.Models;
using NagmClinic.Models.DataTables;
using NagmClinic.ViewModels;

namespace NagmClinic.Services.Pharmacy
{
    public interface IPharmacyPurchasesService
    {
        Task<DataTablesResponse<object>> GetPurchasesDataAsync(DataTablesParameters dtParams);
        Task<PharmacyPurchase?> GetPurchaseDetailsAsync(int id);
        Task<PharmacyPurchaseCreateViewModel> BuildCreateViewModelAsync();
        Task<PurchaseExecutionResult> ExecutePurchaseAsync(PharmacyPurchaseCreateViewModel model);
        Task<PurchaseExecutionResult> EditPurchaseAsync(PharmacyPurchaseEditViewModel model);
        Task<string> GenerateBatchNumberAsync();
        Task<string> GenerateBarcodeAsync();
    }
}
