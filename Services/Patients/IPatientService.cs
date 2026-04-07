using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NagmClinic.Models;
using NagmClinic.Models.DataTables;
using NagmClinic.ViewModels;

namespace NagmClinic.Services.Patients
{
    public interface IPatientService
    {
        Task<DataTablesResponse<object>> GetPatientsDataAsync(DataTablesParameters dtParams);
        Task<Patient?> GetPatientDetailsAsync(int id);
        Task<Patient?> GetPatientByIdAsync(int id);
        Task<ServiceResult> CreatePatientAsync(Patient patient);
        Task<ServiceResult> UpdatePatientAsync(Patient patient);
        Task<IEnumerable<object>> SearchPatientsAsync(string query);
        Task<ServiceResult<Patient>> QuickCreatePatientAsync(string fullName, string phoneNumber, string genderString, string address, int age);
        Task<ServiceResult> QuickEditPatientAsync(int id, string fullName, string phoneNumber, string genderString, string address, int age, bool isActive);
    }
}
