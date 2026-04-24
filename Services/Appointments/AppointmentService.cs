using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using NagmClinic.Data;
using NagmClinic.Extensions;
using NagmClinic.Models;
using NagmClinic.Models.Enums;
using NagmClinic.ViewModels;
using NagmClinic.Models.DataTables;

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
                .Include(a => a.AppointmentItems)
                    .ThenInclude(ai => ai.LabResult)
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
                    AppointmentItemId = ai.Id,
                    RowVersion = ai.RowVersion,
                    ServiceId = ai.ServiceId,
                    ServiceName = ai.Service?.NameAr ?? "",
                    ServiceDisplayName = LabDisplayExtensions.BuildOperationalTestDisplay(
                        ai.Service?.Code,
                        ai.Service?.NameEn,
                        ai.Service?.NameAr),
                    Quantity = ai.Quantity,
                    UnitPrice = ai.UnitPrice,
                    HasRecordedLabResult = HasRecordedLabResult(ai.LabResult),
                    ItemType = ai.Service?.Type ?? ServiceType.Service
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
                .Select(s => new { s.Id, s.NameAr, s.NameEn, s.Code, s.Price, s.Type })
                .ToListAsync();

            model.AvailableServices = services.Select(s => new SelectListItem
            {
                Value = s.Id.ToString(),
                Text = $"{LabDisplayExtensions.BuildOperationalTestDisplay(s.Code, s.NameEn, s.NameAr)} ({s.Price:N2})"
            }).ToList();

            model.ServicesData = services.Select(s => new ServiceItemDto
            {
                Id = s.Id,
                NameAr = s.NameAr,
                NameEn = s.NameEn,
                Code = s.Code,
                DisplayName = LabDisplayExtensions.BuildOperationalTestDisplay(s.Code, s.NameEn, s.NameAr),
                Price = s.Price,
                Type = s.Type
            }).ToList();
        }

        public async Task<DataTablesResponse<object>> GetAppointmentsDataTableAsync(DataTablesParameters dtParams)
        {
            try
            {
                string searchValue = dtParams.Search["value"];

                var query = _context.Appointments
                    .Include(a => a.Patient)
                    .Include(a => a.Doctor)
                    .AsQueryable();

                // Unified Search Logic (Patient Name, Phone, or Daily Number)
                if (!string.IsNullOrEmpty(searchValue))
                {
                    query = query.Where(m => (m.Patient != null && (
                                                m.Patient.FullName.Contains(searchValue) ||
                                                m.Patient.PhoneNumber.Contains(searchValue))) ||
                                            m.DailyNumber.ToString().Contains(searchValue));
                }

                int recordsTotal = await query.CountAsync();

                // 3. Sorting Logic
                if (dtParams.Order.Any())
                {
                    var sort = dtParams.Order.First();
                    bool isAsc = sort.Dir == "asc";

                    switch (sort.Column)
                    {
                        case 0: // DailyNumber
                            query = isAsc ? query.OrderBy(x => x.DailyNumber) : query.OrderByDescending(x => x.DailyNumber);
                            break;
                        case 1: // Date
                            query = isAsc ? query.OrderBy(x => x.AppointmentDate) : query.OrderByDescending(x => x.AppointmentDate);
                            break;
                        case 2: // Patient
                            query = isAsc
                                ? query.OrderBy(x => x.Patient != null ? x.Patient.FullName : string.Empty)
                                : query.OrderByDescending(x => x.Patient != null ? x.Patient.FullName : string.Empty);
                            break;
                        case 3: // Doctor
                            query = isAsc
                                ? query.OrderBy(x => x.Doctor != null ? x.Doctor.NameAr : string.Empty)
                                : query.OrderByDescending(x => x.Doctor != null ? x.Doctor.NameAr : string.Empty);
                            break;
                        case 4: // Status
                            query = isAsc ? query.OrderBy(x => x.Status) : query.OrderByDescending(x => x.Status);
                            break;
                        default:
                            query = query.OrderByDescending(x => x.AppointmentDate).ThenByDescending(x => x.DailyNumber);
                            break;
                    }
                }
                else
                {
                    query = query.OrderByDescending(x => x.AppointmentDate).ThenByDescending(x => x.DailyNumber);
                }

                var data = await query
                    .Skip(dtParams.Start)
                    .Take(dtParams.Length)
                    .Select(a => new
                    {
                        a.Id,
                        a.DailyNumber,
                        AppointmentDate = a.AppointmentDate.ToString("yyyy-MM-dd"),
                        PatientName = a.Patient != null ? a.Patient.FullName : string.Empty,
                        PatientPhone = a.Patient != null ? a.Patient.PhoneNumber : string.Empty,
                        DoctorName = a.Doctor != null ? a.Doctor.NameAr : string.Empty,
                        Status = a.Status.ToString(),
                        BillableTotal = a.Status == AppointmentStatus.Confirmed 
                            ? a.ConsultationFee + (a.AppointmentItems != null ? a.AppointmentItems.Sum(i => i.TotalPrice) : 0m) 
                            : 0m
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
            catch (Exception ex)
            {
                return new DataTablesResponse<object> { error = ex.Message };
            }
        }

        public async Task<(bool Success, string Message, int? AppointmentId)> CreateAppointmentAsync(AppointmentCreateViewModel model)
        {
            try
            {
                var strategy = _context.Database.CreateExecutionStrategy();
                return await strategy.ExecuteAsync(async () =>
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
                                    TotalPrice = item.Quantity * item.UnitPrice,
                                    Status = PaymentStatus.Pending
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
                                        NormalRange = !string.IsNullOrWhiteSpace(service.ReferenceRange)
                                            ? service.ReferenceRange
                                            : service.NormalRange,
                                        RequestedAt = DateTime.Now
                                    };
                                    _context.LabResults.Add(labResult);
                                }
                            }
                            await _context.SaveChangesAsync();
                        }

                        await transaction.CommitAsync();
                        return (true, "تم حفظ الموعد بنجاح", appointment.Id);
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                });
            }
            catch (Exception ex)
            {
                return (false, "حدث خطأ أثناء حفظ الموعد: " + ex.Message, null);
            }
        }

        public async Task<(bool Success, string Message)> UpdateAppointmentAsync(AppointmentCreateViewModel model)
        {
            try
            {
                var strategy = _context.Database.CreateExecutionStrategy();
                return await strategy.ExecuteAsync(async () =>
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();
                    try
                    {
                        var appointment = await _context.Appointments
                            .Include(a => a.AppointmentItems)
                                .ThenInclude(ai => ai.Service)
                            .Include(a => a.AppointmentItems)
                                .ThenInclude(ai => ai.LabResult)
                            .FirstOrDefaultAsync(a => a.Id == model.Id);

                        if (appointment == null) return (false, "الموعد غير موجود");

                        appointment.ConsultationFee = model.ConsultationFee;
                        appointment.ZeroFeeReason = model.ConsultationFee == 0 ? model.ZeroFeeReason : null;
                        appointment.Notes = model.Notes;

                        var existingItems = appointment.AppointmentItems.ToList();
                        var incomingItems = model.Items ?? new List<AppointmentItemViewModel>();
                        var incomingById = incomingItems
                            .Where(i => i.AppointmentItemId > 0)
                            .GroupBy(i => i.AppointmentItemId)
                            .ToDictionary(g => g.Key, g => g.First());
                        var lockedItems = existingItems
                            .Where(i => HasRecordedLabResult(i.LabResult))
                            .ToList();

                        foreach (var lockedItem in lockedItems)
                        {
                            if (!incomingById.TryGetValue(lockedItem.Id, out var incomingLockedItem))
                            {
                                return (false, "لا يمكن تعديل أو حذف الفحوصات التي تم إدخال نتائجها مسبقاً.");
                            }

                            if (incomingLockedItem.ServiceId != lockedItem.ServiceId ||
                                incomingLockedItem.Quantity != lockedItem.Quantity ||
                                incomingLockedItem.UnitPrice != lockedItem.UnitPrice)
                            {
                                return (false, "لا يمكن تعديل أو حذف الفحوصات التي تم إدخال نتائجها مسبقاً.");
                            }
                        }

                        // 1. Remove items that are no longer in the request
                        var incomingItemIds = incomingItems
                            .Where(i => i.AppointmentItemId > 0)
                            .Select(i => i.AppointmentItemId)
                            .ToHashSet();
                        var itemsToRemove = existingItems
                            .Where(ei => !incomingItemIds.Contains(ei.Id))
                            .ToList();
                        _context.AppointmentItems.RemoveRange(itemsToRemove);

                        // 2. Add or Update
                        foreach (var item in incomingItems)
                        {
                            var existingItem = item.AppointmentItemId > 0
                                ? existingItems.FirstOrDefault(ei => ei.Id == item.AppointmentItemId)
                                : null;

                            if (existingItem != null)
                            {
                                // Concurrency check
                                _context.Entry(existingItem).Property("RowVersion").OriginalValue = item.RowVersion;

                                var serviceChanged = existingItem.ServiceId != item.ServiceId;

                                existingItem.ServiceId = item.ServiceId;
                                existingItem.Quantity = item.Quantity;
                                existingItem.UnitPrice = item.UnitPrice;
                                existingItem.TotalPrice = item.Quantity * item.UnitPrice;

                                if (serviceChanged)
                                {
                                    var updatedService = await _context.ClinicServices.FindAsync(item.ServiceId);
                                    if (updatedService != null && updatedService.Type == ServiceType.LabTest)
                                    {
                                        if (existingItem.LabResult == null)
                                        {
                                            _context.LabResults.Add(new LabResult
                                            {
                                                AppointmentItemId = existingItem.Id,
                                                Status = LabStatus.Pending,
                                                Unit = updatedService.Unit,
                                                NormalRange = !string.IsNullOrWhiteSpace(updatedService.ReferenceRange)
                                                    ? updatedService.ReferenceRange
                                                    : updatedService.NormalRange,
                                                RequestedAt = DateTime.Now
                                            });
                                        }
                                        else
                                        {
                                            existingItem.LabResult.ResultValue = null;
                                            existingItem.LabResult.Unit = updatedService.Unit;
                                            existingItem.LabResult.NormalRange = !string.IsNullOrWhiteSpace(updatedService.ReferenceRange)
                                                ? updatedService.ReferenceRange
                                                : updatedService.NormalRange;
                                            existingItem.LabResult.Status = LabStatus.Pending;
                                            existingItem.LabResult.PerformedBy = null;
                                            existingItem.LabResult.PerformedAt = null;
                                            existingItem.LabResult.LabNotes = null;
                                            existingItem.LabResult.RequestedAt = DateTime.Now;
                                        }
                                    }
                                    else if (existingItem.LabResult != null)
                                    {
                                        _context.LabResults.Remove(existingItem.LabResult);
                                        existingItem.LabResult = null;
                                    }
                                }
                            }
                            else
                            {
                                // Add new item
                                var appointmentItem = new AppointmentItem
                                {
                                    AppointmentId = appointment.Id,
                                    ServiceId = item.ServiceId,
                                    Quantity = item.Quantity,
                                    UnitPrice = item.UnitPrice,
                                    TotalPrice = item.Quantity * item.UnitPrice,
                                    Status = PaymentStatus.Pending
                                };
                                _context.AppointmentItems.Add(appointmentItem);

                                // Save changes to get the AppointmentItem.Id generated for LabResult linking
                                await _context.SaveChangesAsync();

                                var service = await _context.ClinicServices.FindAsync(item.ServiceId);
                                if (service != null && service.Type == ServiceType.LabTest)
                                {
                                    var labResult = new LabResult
                                    {
                                        AppointmentItemId = appointmentItem.Id,
                                        Status = LabStatus.Pending,
                                        Unit = service.Unit,
                                        NormalRange = !string.IsNullOrWhiteSpace(service.ReferenceRange)
                                            ? service.ReferenceRange
                                            : service.NormalRange,
                                        RequestedAt = DateTime.Now
                                    };
                                    _context.LabResults.Add(labResult);
                                }
                            }
                        }

                        try
                        {
                            await _context.SaveChangesAsync();
                        }
                        catch (DbUpdateConcurrencyException)
                        {
                            await transaction.RollbackAsync();
                            throw new NagmClinic.Exceptions.ConcurrencyException("هذا السجل تم تعديله بواسطة مستخدم آخر. يرجى تحديث الصفحة والمحاولة مرة أخرى.");
                        }

                        await transaction.CommitAsync();
                        return (true, "تم التعديل بنجاح");
                    }
                    catch
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                });
            }
            catch (NagmClinic.Exceptions.ConcurrencyException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return (false, "حدث خطأ أثناء التعديل: " + ex.Message);
            }
        }

        private static bool HasRecordedLabResult(LabResult? labResult)
        {
            return labResult != null &&
                   (!string.IsNullOrWhiteSpace(labResult.ResultValue) ||
                    labResult.Status == LabStatus.Completed ||
                    labResult.PerformedAt.HasValue);
        }

        public async Task<(bool Success, string Message)> RefundAppointmentItemAsync(int itemId)
        {
            try
            {
                var item = await _context.AppointmentItems.FindAsync(itemId);
                if (item == null) return (false, "الخدمة غير موجودة");

                if (item.Status == PaymentStatus.Refunded)
                    return (false, "هذه الخدمة مسترجعة بالفعل");

                item.Status = PaymentStatus.Refunded;
                item.TotalPrice = 0; // Zero out the financial impact

                await _context.SaveChangesAsync();
                return (true, "تم استرجاع المبلغ بنجاح");
            }
            catch (Exception ex)
            {
                return (false, "حدث خطأ أثناء الاسترجاع: " + ex.Message);
            }
        }
    }
}
