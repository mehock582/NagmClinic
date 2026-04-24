using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
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
    [Authorize]
    public class AppointmentsController : BaseController
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
            var dtParams = Request.GetDataTablesParameters();
            var response = await _appointmentService.GetAppointmentsDataTableAsync(dtParams);
            return Json(response);
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
                ShowAlert(result.Message);
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
            await PopulateEditMetadataAsync(model);

            return View(model);
        }

        // POST: Appointments/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, AppointmentCreateViewModel model)
        {
            if (id != model.Id) return NotFound();

            if (model.Items != null)
            {
                for (int i = 0; i < model.Items.Count; i++)
                {
                    if (model.Items[i].AppointmentItemId == 0) // New item
                    {
                        ModelState.Remove($"Items[{i}].RowVersion");
                    }
                }
            }

            if (!ModelState.IsValid)
            {
                ViewData["FormErrorMessage"] = "تعذر حفظ التعديلات، يرجى مراجعة البيانات المدخلة.";
                return View(await RebuildEditViewModelAsync(id, model));
            }

            var result = await _appointmentService.UpdateAppointmentAsync(model);
            if (result.Success)
            {
                ShowAlert("تم تحديث بيانات الموعد بنجاح");
                return RedirectToAction(nameof(Index));
            }

            ModelState.AddModelError("", result.Message);
            ViewData["FormErrorMessage"] = result.Message;
            return View(await RebuildEditViewModelAsync(id, model));
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
        [Authorize(Roles = "Cashier,Admin")]
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

        [HttpPost]
        [Authorize(Roles = "Cashier,Admin")]
        public async Task<IActionResult> RefundItem(int id)
        {
            var result = await _appointmentService.RefundAppointmentItemAsync(id);
            return Json(new { success = result.Success, message = result.Message });
        }

        private async Task PopulateEditMetadataAsync(AppointmentCreateViewModel model)
        {
            var patient = await _context.Patients.FirstOrDefaultAsync(p => p.Id == model.PatientId);
            var doctor = await _context.Doctors.FirstOrDefaultAsync(d => d.Id == model.DoctorId);
            var appointment = await _context.Appointments.FindAsync(model.Id);

            model.PatientName = patient?.FullName;
            model.DoctorName = doctor?.NameAr;
            model.DailyNumber = (int?)appointment?.DailyNumber;
        }

        private async Task<AppointmentCreateViewModel> RebuildEditViewModelAsync(int id, AppointmentCreateViewModel postedModel)
        {
            var rebuiltModel = await _appointmentService.BuildEditViewModelAsync(id) ?? postedModel;
            rebuiltModel.ConsultationFee = postedModel.ConsultationFee;
            rebuiltModel.Notes = postedModel.Notes;
            rebuiltModel.ZeroFeeReason = postedModel.ZeroFeeReason;
            await PopulateEditMetadataAsync(rebuiltModel);
            return rebuiltModel;
        }
    }
}

