using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NagmClinic.Data;
using NagmClinic.Models.DataTables;

namespace NagmClinic.Services.Pharmacy
{
    public class PharmacyMasterDataService : IPharmacyMasterDataService
    {
        private readonly ApplicationDbContext _context;

        public PharmacyMasterDataService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<DataTablesResponse<object>> GetCategoriesDataAsync(DataTablesParameters dtParams)
        {
            var query = _context.PharmacyCategories.AsQueryable();
            var searchValue = dtParams.Search != null && dtParams.Search.ContainsKey("value") ? dtParams.Search["value"] : null;
            
            if (!string.IsNullOrEmpty(searchValue))
                query = query.Where(c => c.Name.Contains(searchValue));

            int recordsTotal = await query.CountAsync();
            var data = await query.OrderBy(c => c.Name)
                .Skip(dtParams.Start)
                .Take(dtParams.Length)
                .Select(c => new { c.Id, c.Name, c.Description, c.IsActive })
                .ToListAsync();

            return new DataTablesResponse<object> { draw = dtParams.Draw, recordsTotal = recordsTotal, recordsFiltered = recordsTotal, data = data };
        }

        public async Task<DataTablesResponse<object>> GetLocationsDataAsync(DataTablesParameters dtParams)
        {
            var query = _context.PharmacyLocations.AsQueryable();
            var searchValue = dtParams.Search != null && dtParams.Search.ContainsKey("value") ? dtParams.Search["value"] : null;
            
            if (!string.IsNullOrEmpty(searchValue))
                query = query.Where(l => l.Code.Contains(searchValue) || l.Description.Contains(searchValue));

            int recordsTotal = await query.CountAsync();
            var data = await query.OrderBy(l => l.Code)
                .Skip(dtParams.Start)
                .Take(dtParams.Length)
                .Select(l => new { l.Id, l.Code, l.Description, l.IsActive })
                .ToListAsync();

            return new DataTablesResponse<object> { draw = dtParams.Draw, recordsTotal = recordsTotal, recordsFiltered = recordsTotal, data = data };
        }

        public async Task<DataTablesResponse<object>> GetSuppliersDataAsync(DataTablesParameters dtParams)
        {
            var query = _context.PharmacySuppliers.AsQueryable();
            var searchValue = dtParams.Search != null && dtParams.Search.ContainsKey("value") ? dtParams.Search["value"] : null;
            
            if (!string.IsNullOrEmpty(searchValue))
                query = query.Where(s => s.Name.Contains(searchValue) || (s.PhoneNumber != null && s.PhoneNumber.Contains(searchValue)));

            int recordsTotal = await query.CountAsync();
            var data = await query.OrderBy(s => s.Name)
                .Skip(dtParams.Start)
                .Take(dtParams.Length)
                .Select(s => new { s.Id, s.Name, s.ContactPerson, s.PhoneNumber, s.Address, s.Notes, s.IsActive })
                .ToListAsync();

            return new DataTablesResponse<object> { draw = dtParams.Draw, recordsTotal = recordsTotal, recordsFiltered = recordsTotal, data = data };
        }

        public async Task<DataTablesResponse<object>> GetUnitsDataAsync(DataTablesParameters dtParams)
        {
            var query = _context.PharmacyUnits.AsQueryable();
            var searchValue = dtParams.Search != null && dtParams.Search.ContainsKey("value") ? dtParams.Search["value"] : null;
            
            if (!string.IsNullOrEmpty(searchValue))
                query = query.Where(u => u.Name.Contains(searchValue));

            int recordsTotal = await query.CountAsync();
            var data = await query.OrderBy(u => u.Name)
                .Skip(dtParams.Start)
                .Take(dtParams.Length)
                .Select(u => new { u.Id, u.Name, u.NameEn, u.IsActive })
                .ToListAsync();

            return new DataTablesResponse<object> { draw = dtParams.Draw, recordsTotal = recordsTotal, recordsFiltered = recordsTotal, data = data };
        }
    }
}
