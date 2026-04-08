using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using NagmClinic.Data;
using NagmClinic.Models;
using NagmClinic.Models.Enums;
using NagmClinic.ViewModels;

namespace NagmClinic.Services.Appointments
{
    public class AppointmentService : IAppointmentService
    {
        private readonly ApplicationDbContext _context;

        public AppointmentService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<AppointmentCreateViewModel> BuildCreateViewModelAsync()
        {
            var model = new AppointmentCreateViewModel();
            await PopulateDropdownsAsync(model);
            return model;
        }

        public async Task<AppointmentCreateViewModel?> BuildEditViewModelAsync(int appointmentId)
        {
            var appointment = await _context.Appointments
                .Include(a => a.AppointmentItems)
                    .ThenInclude(ai => ai.Service)
                .FirstOrDefaultAsync(m => m.Id == appointmentId);

            if (appointment == null) return null;

            var model = new AppointmentCreateViewModel
            {
                Id = appointment.Id,
                PatientId = appointment.PatientId,
                DoctorId = appointment.DoctorId,
                AppointmentDate = appointment.AppointmentDate,
                ConsultationFee = appointment.ConsultationFee,
                ZeroFeeReason = appointment.ZeroFeeReason,
                Notes = appointment.Notes,
                Items = appointment.AppointmentItems.Select(ai => new AppointmentItemViewModel
                {
                    ServiceId = ai.ServiceId,
                    ServiceName = ai.Service?.NameAr ?? "",
                    Quantity = ai.Quantity,
                    UnitPrice = ai.UnitPrice
                }).ToList()
            };

            await PopulateDropdownsAsync(model);
            return model;
        }

        private async Task PopulateDropdownsAsync(AppointmentCreateViewModel model)
        {
            var doctors = await _context.Doctors
                .Where(d => d.IsActive)
                .Select(d => new { d.Id, d.NameAr, d.ConsultationFee })
                .ToListAsync();

            model.AvailableDoctors = doctors.Select(d => new SelectListItem
            {
                Value = d.Id.ToString(),
                Text = d.NameAr
            }).ToList();
            
            model.DoctorFees = doctors.ToDictionary(d => d.Id, d => d.ConsultationFee);

            var services = await _context.ClinicServices
                .Where(s => s.IsActive)
                .Select(s => new { s.Id, s.NameAr, s.Price })
                .ToListAsync();

            model.AvailableServices = services.Select(s => new SelectListItem
            {
                Value = s.Id.ToString(),
                Text = $"{s.NameAr} ({s.Price:N2})"
            }).ToList();

            model.ServicesData = services.Select(s => new ServiceItemDto
            {
                Id = s.Id,
                NameAr = s.NameAr,
                Price = s.Price
            }).ToList();
        }

        public async Task<(bool Success, string Message, int? AppointmentId)> CreateAppointmentAsync(AppointmentCreateViewModel model)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Generate Daily Number: yyMMddNN
                var today = model.AppointmentDate.Date;
                string dateStr = today.ToString("yyMMdd");
                long baseNumber = long.Parse(dateStr) * 100;

                var maxDailyToday = await _context.Appointments
                    .Where(a => a.AppointmentDate.Date == today)
                    .MaxAsync(a => (long?)a.DailyNumber) ?? 0;

                long nextNumber = Math.Max(maxDailyToday, baseNumber) + 1;

                var appointment = new Appointment
                {
                    AppointmentDate = model.AppointmentDate,
                    DailyNumber = nextNumber,
                    PatientId = model.PatientId,
                    DoctorId = model.DoctorId,
                    ConsultationFee = model.ConsultationFee,
                    ZeroFeeReason = model.ConsultationFee == 0 ? model.ZeroFeeReason : null,
                    Notes = model.Notes,
                    Status = AppointmentStatus.Confirmed,
                    CreatedAt = DateTime.Now
                };

                _context.Appointments.Add(appointment);
                await _context.SaveChangesAsync();

                if (model.Items != null && model.Items.Any())
                {
                    foreach (var item in model.Items)
                    {
                        var appointmentItem = new AppointmentItem
                        {
                            AppointmentId = appointment.Id,
                            ServiceId = item.ServiceId,
                            Quantity = item.Quantity,
                            UnitPrice = item.UnitPrice,
                            TotalPrice = item.Quantity * item.UnitPrice
                        };
                        _context.AppointmentItems.Add(appointmentItem);
                        await _context.SaveChangesAsync();

                        var service = await _context.ClinicServices.FindAsync(item.ServiceId);
                        if (service != null && service.Type == ServiceType.LabTest)
                        {
                            var labResult = new LabResult
                            {
                                AppointmentItemId = appointmentItem.Id,
                                Status = LabStatus.Pending,
                                Unit = service.Unit,
                                NormalRange = service.NormalRange
                            };
                            _context.LabResults.Add(labResult);
                        }
                    }
                    await _context.SaveChangesAsync();
                }

                await transaction.CommitAsync();
                return (true, "تم حفظ الموعد بنجاح", appointment.Id);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, "حدث خطأ أثناء حفظ الموعد: " + ex.Message, null);
            }
        }

        public async Task<(bool Success, string Message)> UpdateAppointmentAsync(AppointmentCreateViewModel model)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var appointment = await _context.Appointments
                    .Include(a => a.AppointmentItems)
                    .FirstOrDefaultAsync(a => a.Id == model.Id);
                
                if (appointment == null) return (false, "الموعد غير موجود");

                appointment.ConsultationFee = model.ConsultationFee;
                appointment.ZeroFeeReason = model.ConsultationFee == 0 ? model.ZeroFeeReason : null;
                appointment.Notes = model.Notes;
                
                _context.AppointmentItems.RemoveRange(appointment.AppointmentItems);

                if (model.Items != null && model.Items.Any())
                {
                    foreach (var item in model.Items)
                    {
                        var appointmentItem = new AppointmentItem
                        {
                            AppointmentId = appointment.Id,
                            ServiceId = item.ServiceId,
                            Quantity = item.Quantity,
                            UnitPrice = item.UnitPrice,
                            TotalPrice = item.Quantity * item.UnitPrice
                        };
                        _context.AppointmentItems.Add(appointmentItem);
                        await _context.SaveChangesAsync();

                        var service = await _context.ClinicServices.FindAsync(item.ServiceId);
                        if (service != null && service.Type == ServiceType.LabTest)
                        {
                            var labResult = new LabResult
                            {
                                AppointmentItemId = appointmentItem.Id,
                                Status = LabStatus.Pending,
                                Unit = service.Unit,
                                NormalRange = service.NormalRange
                            };
                            _context.LabResults.Add(labResult);
                        }
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return (true, "تم التعديل بنجاح");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, "حدث خطأ أثناء التعديل: " + ex.Message);
            }
        }
    }
}
