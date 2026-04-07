using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using NagmClinic.Data;
using NagmClinic.Models;
using NagmClinic.Models.DataTables;
using NagmClinic.ViewModels;

namespace NagmClinic.Services.Patients
{
    public class PatientService : IPatientService
    {
        private readonly ApplicationDbContext _context;

        public PatientService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<DataTablesResponse<object>> GetPatientsDataAsync(DataTablesParameters dtParams)
        {
            var query = _context.Patients.AsQueryable();

            var searchValue = dtParams.Search != null && dtParams.Search.ContainsKey("value") ? dtParams.Search["value"] : null;

            if (!string.IsNullOrEmpty(searchValue))
            {
                query = query.Where(p => p.FullName.Contains(searchValue) 
                                      || p.PhoneNumber.Contains(searchValue));
            }

            int recordsTotal = await query.CountAsync();

            var data = await query
                .OrderByDescending(p => p.CreatedAt)
                .Skip(dtParams.Start)
                .Take(dtParams.Length)
                .Select(p => new
                {
                    p.Id,
                    p.FullName,
                    p.PhoneNumber,
                    p.Age,
                    Gender = p.Gender.ToString(),
                    p.Address,
                    p.IsActive
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

        public async Task<Patient?> GetPatientDetailsAsync(int id)
        {
            return await _context.Patients
                .Include(p => p.Appointments)
                    .ThenInclude(a => a.Doctor)
                .Include(p => p.Appointments)
                    .ThenInclude(a => a.AppointmentItems)
                        .ThenInclude(ai => ai.Service)
                .FirstOrDefaultAsync(m => m.Id == id);
        }

        public async Task<Patient?> GetPatientByIdAsync(int id)
        {
            return await _context.Patients.FindAsync(id);
        }

        public async Task<ServiceResult> CreatePatientAsync(Patient patient)
        {
            bool exists = await _context.Patients.AnyAsync(p => p.PhoneNumber == patient.PhoneNumber);
            if (exists)
            {
                return ServiceResult.Error("عذراً، رقم الهاتف هذا مسجل مسبقاً لمريض آخر");
            }

            patient.CreatedAt = DateTime.Now;
            patient.IsActive = true;
            
            _context.Add(patient);
            await _context.SaveChangesAsync();
            return ServiceResult.SuccessResult("تمت إضافة المريض بنجاح");
        }

        public async Task<ServiceResult> UpdatePatientAsync(Patient patient)
        {
            try
            {
                bool exists = await _context.Patients.AnyAsync(p => p.PhoneNumber == patient.PhoneNumber && p.Id != patient.Id);
                if (exists)
                {
                    return ServiceResult.Error("رقم الهاتف المدخل مسجل لمريض آخر");
                }

                _context.Update(patient);
                await _context.SaveChangesAsync();
                return ServiceResult.SuccessResult("تم تحديث بيانات المريض بنجاح");
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.Patients.AnyAsync(e => e.Id == patient.Id)) return ServiceResult.Error("المريض غير موجود");
                else throw;
            }
        }

        public async Task<IEnumerable<object>> SearchPatientsAsync(string query)
        {
            if (string.IsNullOrEmpty(query)) return new List<object>();

            return await _context.Patients
                .Where(p => p.FullName.Contains(query) || p.PhoneNumber.Contains(query))
                .Select(p => new { p.Id, p.FullName, p.PhoneNumber })
                .Take(10)
                .ToListAsync();
        }

        public async Task<ServiceResult<Patient>> QuickCreatePatientAsync(string fullName, string phoneNumber, string genderString, string address, int age)
        {
            if (string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(phoneNumber))
            {
                return ServiceResult<Patient>.Error("الاسم ورقم الهاتف مطلوبان");
            }

            if (await _context.Patients.AnyAsync(p => p.PhoneNumber == phoneNumber))
            {
                return ServiceResult<Patient>.Error("رقم الهاتف مسجل لمريض آخر بالفعل");
            }

            if (!Enum.TryParse(typeof(Models.Enums.Gender), genderString, out var genderObj))
            {
                genderObj = Models.Enums.Gender.Male;
            }

            var patient = new Patient
            {
                FullName = fullName,
                PhoneNumber = phoneNumber,
                Gender = (Models.Enums.Gender)genderObj,
                Address = address,
                Age = age,
                CreatedAt = DateTime.Now,
                IsActive = true
            };

            _context.Patients.Add(patient);
            await _context.SaveChangesAsync();

            return ServiceResult<Patient>.SuccessResult(patient, "تم إضافة المريض بنجاح");
        }

        public async Task<ServiceResult> QuickEditPatientAsync(int id, string fullName, string phoneNumber, string genderString, string address, int age, bool isActive)
        {
            if (string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(phoneNumber))
                return ServiceResult.Error("الاسم ورقم الهاتف مطلوبان");

            if (await _context.Patients.AnyAsync(p => p.PhoneNumber == phoneNumber && p.Id != id))
                return ServiceResult.Error("رقم الهاتف مسجل لمريض آخر بالفعل");

            var patient = await _context.Patients.FindAsync(id);
            if (patient == null) return ServiceResult.Error("لم يتم العثور على المريض");

            if (!Enum.TryParse(typeof(Models.Enums.Gender), genderString, out var genderObj))
                genderObj = Models.Enums.Gender.Male;

            patient.FullName = fullName;
            patient.PhoneNumber = phoneNumber;
            patient.Gender = (Models.Enums.Gender)genderObj;
            patient.Address = address;
            patient.Age = age;
            patient.IsActive = isActive;
            
            _context.Update(patient);
            await _context.SaveChangesAsync();

            return ServiceResult.SuccessResult("تم تحديث بيانات المريض بنجاح");
        }
    }
}
