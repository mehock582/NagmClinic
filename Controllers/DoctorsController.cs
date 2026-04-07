using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NagmClinic.Data;
using NagmClinic.Models;
using NagmClinic.ViewModels;
using NagmClinic.Extensions;
using NagmClinic.Models.DataTables;

namespace NagmClinic.Controllers
{
    public class DoctorsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DoctorsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Doctors
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> GetDoctorsData()
        {
            try
            {
                var dtParams = Request.GetDataTablesParameters();

                var query = _context.Doctors.AsQueryable();

                var searchValue = dtParams.Search != null && dtParams.Search.ContainsKey("value") ? dtParams.Search["value"] : null;

                if (!string.IsNullOrEmpty(searchValue))
                {
                    query = query.Where(d => d.NameAr.Contains(searchValue) 
                                          || d.PhoneNumber.Contains(searchValue)
                                          || d.Specialty.Contains(searchValue));
                }

                int recordsTotal = await query.CountAsync();

                var data = await query
                    .OrderByDescending(d => d.CreatedAt)
                    .Skip(dtParams.Start)
                    .Take(dtParams.Length)
                    .Select(d => new
                    {
                        d.Id,
                        d.NameAr,
                        d.PhoneNumber,
                        d.Specialty,
                        d.ConsultationFee,
                        d.RoomNumber,
                        d.IsActive
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

        // GET: Doctors/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var doctor = await _context.Doctors
                .Include(d => d.Appointments)
                    .ThenInclude(a => a.Patient)
                .FirstOrDefaultAsync(m => m.Id == id);

            if (doctor == null) return NotFound();

            return View(doctor);
        }

        // GET: Doctors/Create
        public IActionResult Create()
        {
            return View(new DoctorViewModel());
        }

        // POST: Doctors/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(DoctorViewModel model)
        {
            if (ModelState.IsValid)
            {
                var doctor = new Doctor
                {
                    NameAr = model.NameAr,
                    NameEn = model.NameEn,
                    Specialty = model.Specialty,
                    PhoneNumber = model.PhoneNumber,
                    ConsultationFee = model.ConsultationFee,
                    RoomNumber = model.RoomNumber,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };

                _context.Add(doctor);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        // GET: Doctors/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var doctor = await _context.Doctors.FindAsync(id);
            if (doctor == null) return NotFound();

            var model = new DoctorViewModel
            {
                Id = doctor.Id,
                NameAr = doctor.NameAr,
                NameEn = doctor.NameEn,
                Specialty = doctor.Specialty,
                PhoneNumber = doctor.PhoneNumber,
                RoomNumber = doctor.RoomNumber,
                ConsultationFee = doctor.ConsultationFee,
                IsActive = doctor.IsActive
            };

            return View(model);
        }

        // POST: Doctors/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, DoctorViewModel model)
        {
            if (id != model.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var doctor = await _context.Doctors.FindAsync(id);
                    if (doctor == null) return NotFound();

                    doctor.NameAr = model.NameAr;
                    doctor.NameEn = model.NameEn;
                    doctor.Specialty = model.Specialty;
                    doctor.PhoneNumber = model.PhoneNumber;
                    doctor.ConsultationFee = model.ConsultationFee;
                    doctor.RoomNumber = model.RoomNumber;
                    doctor.IsActive = model.IsActive;

                    _context.Update(doctor);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!DoctorExists(model.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        // AJAX POST: Doctors/QuickCreate
        [HttpPost]
        public async Task<IActionResult> QuickCreate([FromBody] DoctorViewModel model)
        {
            if (ModelState.IsValid)
            {
                var doctor = new Doctor
                {
                    NameAr = model.NameAr,
                    NameEn = model.NameEn,
                    Specialty = model.Specialty,
                    PhoneNumber = model.PhoneNumber,
                    ConsultationFee = model.ConsultationFee,
                    RoomNumber = model.RoomNumber,
                    IsActive = true,
                    CreatedAt = DateTime.Now
                };

                _context.Doctors.Add(doctor);
                await _context.SaveChangesAsync();
                return Json(new { success = true, doctorId = doctor.Id, name = doctor.NameAr });
            }

            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            return Json(new { success = false, message = string.Join(", ", errors) });
        }

        // AJAX POST: Doctors/QuickEdit
        [HttpPost]
        public async Task<IActionResult> QuickEdit([FromBody] DoctorViewModel model)
        {
            if (ModelState.IsValid)
            {
                var doctor = await _context.Doctors.FindAsync(model.Id);
                if (doctor == null) return Json(new { success = false, message = "لم يتم العثور على الطبيب" });

                doctor.NameAr = model.NameAr;
                doctor.Specialty = model.Specialty;
                doctor.PhoneNumber = model.PhoneNumber;
                doctor.ConsultationFee = model.ConsultationFee;
                doctor.RoomNumber = model.RoomNumber;
                doctor.IsActive = model.IsActive;

                _context.Update(doctor);
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }

            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            return Json(new { success = false, message = string.Join(", ", errors) });
        }

        // AJAX POST: Doctors/ToggleStatus/5
        [HttpPost]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var doctor = await _context.Doctors.FindAsync(id);
            if (doctor == null) return Json(new { success = false });

            doctor.IsActive = !doctor.IsActive;
            await _context.SaveChangesAsync();
            return Json(new { success = true, isActive = doctor.IsActive });
        }

        private bool DoctorExists(int id)
        {
            return _context.Doctors.Any(e => e.Id == id);
        }
    }
}
