using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using NagmClinic.Data;
using NagmClinic.Models;
using NagmClinic.Models.Enums;
using NagmClinic.ViewModels;
using NagmClinic.Extensions;
using NagmClinic.Models.DataTables;

using NagmClinic.Services.Appointments;

namespace NagmClinic.Controllers
{
    public class AppointmentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAppointmentService _appointmentService;

        public AppointmentsController(ApplicationDbContext context, IAppointmentService appointmentService)
        {
            _context = context;
            _appointmentService = appointmentService;
        }

        // GET: Appointments
        public async Task<IActionResult> Index()
        {
            var doctors = await _context.Doctors.Where(d => d.IsActive).ToListAsync();
            // Passing doctors to Index to populate the filter in DataTables UI if needed later, 
            // but for now we'll use the server-side logic in the next method
            ViewData["Doctors"] = new SelectList(doctors, "Id", "NameAr");
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> GetAppointmentsData()
        {
            try
            {
                var dtParams = Request.GetDataTablesParameters();
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

                var response = new DataTablesResponse<object>
                {
                    draw = dtParams.Draw,
                    recordsTotal = recordsTotal,
                    recordsFiltered = recordsTotal,
                    data = data
                };

                return Json(response);
            }
            catch (Exception ex)
            {
                return Json(new DataTablesResponse<object> { error = ex.Message });
            }
        }

        // GET: Appointments/Create
        public async Task<IActionResult> Create()
        {
            var model = await _appointmentService.BuildCreateViewModelAsync();
            return View(model);
        }

        // POST: Appointments/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AppointmentCreateViewModel model)
        {
            if (!ModelState.IsValid)
            {
                var repopulatedModel = await _appointmentService.BuildCreateViewModelAsync();
                model.AvailableDoctors = repopulatedModel.AvailableDoctors;
                model.AvailableServices = repopulatedModel.AvailableServices;
                return View(model);
            }

            var result = await _appointmentService.CreateAppointmentAsync(model);
            if (result.Success)
            {
                if (model.PrintReceipt)
                {
                    return RedirectToAction(nameof(Details), new { id = result.AppointmentId, print = true });
                }
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError("", result.Message);
            var repop = await _appointmentService.BuildCreateViewModelAsync();
            model.AvailableDoctors = repop.AvailableDoctors;
            model.AvailableServices = repop.AvailableServices;
            return View(model);
        }

        // GET: Appointments/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var model = await _appointmentService.BuildEditViewModelAsync(id.Value);
            if (model == null) return NotFound();

            var patient = await _context.Patients.FirstOrDefaultAsync(p => p.Id == model.PatientId);
            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.Id == model.DoctorId);
            var appointment = await _context.Appointments.FindAsync(model.Id);
            
            model.PatientName = patient?.FullName;
            model.DoctorName = doctor?.NameAr;
            model.DailyNumber = (int?)appointment?.DailyNumber;

            return View(model);
        }

        // POST: Appointments/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, AppointmentCreateViewModel model)
        {
            if (id != model.Id) return NotFound();

            if (!ModelState.IsValid)
            {
                var repopulatedModel = await _appointmentService.BuildEditViewModelAsync(id);
                model.AvailableDoctors = repopulatedModel?.AvailableDoctors;
                model.AvailableServices = repopulatedModel?.AvailableServices;
                return View(model);
            }

            var result = await _appointmentService.UpdateAppointmentAsync(model);
            if (result.Success)
            {
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError("", result.Message);
            var repop = await _appointmentService.BuildEditViewModelAsync(id);
            model.AvailableDoctors = repop?.AvailableDoctors;
            model.AvailableServices = repop?.AvailableServices;
            return View(model);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var appointment = await _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.Doctor)
                .Include(a => a.AppointmentItems)
                    .ThenInclude(ai => ai.Service)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (appointment == null) return NotFound();

            return View(appointment);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int id, AppointmentStatus status)
        {
            var appointment = await _context.Appointments
                .Include(a => a.AppointmentItems)
                .FirstOrDefaultAsync(a => a.Id == id);
            if (appointment == null) return Json(new { success = false });

            appointment.Status = status;

            // When cancelled, zero out all billable amounts for tracking purposes
            if (status == AppointmentStatus.Cancelled)
            {
                appointment.ConsultationFee = 0;
                if (appointment.AppointmentItems != null)
                {
                    foreach (var item in appointment.AppointmentItems)
                    {
                        item.UnitPrice = 0;
                        item.TotalPrice = 0;
                    }
                }
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
    }
}

