using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NagmClinic.Data;
using NagmClinic.Models;
using NagmClinic.Models.Enums;
using NagmClinic.ViewModels;
using NagmClinic.Extensions;
using NagmClinic.Models.DataTables;

namespace NagmClinic.Controllers
{
    public class LaboratoryController : Controller
    {
        private readonly ApplicationDbContext _context;

        public LaboratoryController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Laboratory
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> GetLabResultsData()
        {
            try
            {
                var dtParams = Request.GetDataTablesParameters();
                var query = _context.LabResults
                    .Include(lr => lr.AppointmentItem)
                        .ThenInclude(ai => ai.Appointment)
                            .ThenInclude(a => a.Patient)
                    .Include(lr => lr.AppointmentItem)
                        .ThenInclude(ai => ai.Appointment)
                            .ThenInclude(a => a.Doctor)
                    .Include(lr => lr.AppointmentItem)
                        .ThenInclude(ai => ai.Service)
                    .AsQueryable();

                var searchValue = dtParams.Search != null && dtParams.Search.ContainsKey("value") ? dtParams.Search["value"] : null;
                if (!string.IsNullOrEmpty(searchValue))
                {
                    query = query.Where(lr => (lr.AppointmentItem.Appointment.Patient != null &&
                                               lr.AppointmentItem.Appointment.Patient.FullName.Contains(searchValue)) ||
                                           lr.AppointmentItem.Appointment.DailyNumber.ToString().Contains(searchValue));
                }

                int recordsTotal = await query.CountAsync();
                var data = await query
                    .OrderByDescending(lr => lr.AppointmentItem.Appointment.AppointmentDate)
                    .Skip(dtParams.Start).Take(dtParams.Length)
                    .Select(lr => new
                    {
                        lr.Id,
                        DailyNumber = (lr.AppointmentItem != null && lr.AppointmentItem.Appointment != null) ? lr.AppointmentItem.Appointment.DailyNumber.ToString() : "-",
                        AppointmentDate = (lr.AppointmentItem != null && lr.AppointmentItem.Appointment != null) ? lr.AppointmentItem.Appointment.AppointmentDate.ToString("yyyy-MM-dd HH:mm") : "-",
                        PatientName = (lr.AppointmentItem != null && lr.AppointmentItem.Appointment != null && lr.AppointmentItem.Appointment.Patient != null) ? lr.AppointmentItem.Appointment.Patient.FullName : "غير معروف",
                        PatientPhone = (lr.AppointmentItem != null && lr.AppointmentItem.Appointment != null && lr.AppointmentItem.Appointment.Patient != null) ? lr.AppointmentItem.Appointment.Patient.PhoneNumber : "-",
                        DoctorName = (lr.AppointmentItem != null && lr.AppointmentItem.Appointment != null && lr.AppointmentItem.Appointment.Doctor != null) ? lr.AppointmentItem.Appointment.Doctor.NameAr : "",
                        TestName = (lr.AppointmentItem != null && lr.AppointmentItem.Service != null) ? lr.AppointmentItem.Service.NameAr : "-",
                        ResultType = (lr.AppointmentItem != null && lr.AppointmentItem.Service != null) ? (int)lr.AppointmentItem.Service.ResultType : 0,
                        PredefinedValues = (lr.AppointmentItem != null && lr.AppointmentItem.Service != null) ? lr.AppointmentItem.Service.PredefinedValues : "",
                        Status = lr.Status.ToString(),
                        lr.ResultValue,
                        lr.Unit,
                        lr.NormalRange,
                        lr.LabNotes,
                        lr.PerformedBy
                    })
                    .ToListAsync();

                return Json(new DataTablesResponse<object> { draw = dtParams.Draw, recordsTotal = recordsTotal, recordsFiltered = recordsTotal, data = data });
            }
            catch (Exception ex) { return Json(new DataTablesResponse<object> { error = ex.Message }); }
        }

        // GET: Laboratory/Manage/5 (Appointment ID)
        public async Task<IActionResult> Manage(int id)
        {
            var appointment = await _context.Appointments
                .Include(a => a.Patient)
                .Include(a => a.AppointmentItems)
                    .ThenInclude(ai => ai.Service)
                .Include(a => a.AppointmentItems)
                    .ThenInclude(ai => ai.LabResult)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (appointment == null) return NotFound();

            return View(appointment);
        }

        // POST: Laboratory/UpdateResult
        [HttpPost]
        public async Task<IActionResult> UpdateResult(int id, string resultValue, string? unit, string? normalRange, LabStatus status, string? labNotes, string? performedBy)
        {
            var labResult = await _context.LabResults.FindAsync(id);
            if (labResult == null) return Json(new { success = false, message = "لم يتم العثور على الفحص" });

            labResult.ResultValue = resultValue;
            labResult.Unit = unit;
            labResult.NormalRange = normalRange;
            labResult.Status = status;
            labResult.LabNotes = labNotes;
            labResult.PerformedBy = performedBy;
            
            if (status == LabStatus.Completed)
            {
                labResult.PerformedAt = DateTime.Now;
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }
    }
}

