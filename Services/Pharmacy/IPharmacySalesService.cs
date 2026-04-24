using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NagmClinic.Models;
using NagmClinic.Models.DataTables;
using NagmClinic.ViewModels;

namespace NagmClinic.Services.Pharmacy
{
    public interface IPharmacySalesService
    {
        Task<DataTablesResponse<object>> GetSalesDataAsync(DataTablesParameters dtParams);
        Task<PharmacySale?> GetSaleDetailsAsync(int id);
        Task<PharmacySaleCreateViewModel> BuildCreateViewModelAsync();
        Task<SaleExecutionResult> ExecuteSaleAsync(PharmacySaleCreateViewModel model);
        Task<SaleExecutionResult> EditSaleAsync(PharmacySaleEditViewModel model);
    }
}
