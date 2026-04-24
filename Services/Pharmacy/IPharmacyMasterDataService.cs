using System.Threading.Tasks;
using NagmClinic.Models.DataTables;

namespace NagmClinic.Services.Pharmacy
{
    public interface IPharmacyMasterDataService
    {
        Task<DataTablesResponse<object>> GetCategoriesDataAsync(DataTablesParameters dtParams);
        Task<DataTablesResponse<object>> GetLocationsDataAsync(DataTablesParameters dtParams);
        Task<DataTablesResponse<object>> GetSuppliersDataAsync(DataTablesParameters dtParams);
        Task<DataTablesResponse<object>> GetUnitsDataAsync(DataTablesParameters dtParams);
    }
}
