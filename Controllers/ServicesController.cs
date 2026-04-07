using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NagmClinic.Data;
using NagmClinic.Models;
using NagmClinic.Models.Enums;
using NagmClinic.Extensions;
using NagmClinic.Models.DataTables;
using NagmClinic.ViewModels;

namespace NagmClinic.Controllers
{
    public class ServicesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ServicesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Services
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> GetServicesData()
        {
            try
            {
                var dtParams = Request.GetDataTablesParameters();
                var query = _context.ClinicServices.Where(s => s.Type == ServiceType.Service);
                var searchValue = dtParams.Search != null && dtParams.Search.ContainsKey("value") ? dtParams.Search["value"] : null;
                if (!string.IsNullOrEmpty(searchValue))
                    query = query.Where(s => s.NameAr.Contains(searchValue));
                int recordsTotal = await query.CountAsync();
                var data = await query.OrderBy(s => s.NameAr).Skip(dtParams.Start).Take(dtParams.Length)
                    .Select(s => new { s.Id, s.NameAr, s.Price, s.Notes, s.IsActive })
                    .ToListAsync();
                return Json(new DataTablesResponse<object> { draw = dtParams.Draw, recordsTotal = recordsTotal, recordsFiltered = recordsTotal, data = data });
            }
            catch (Exception ex) { return Json(new DataTablesResponse<object> { error = ex.Message }); }
        }

        // AJAX POST: Services/QuickCreate
        [HttpPost]
        public async Task<IActionResult> QuickCreate([FromBody] ClinicServiceViewModel model)
        {
            if (ModelState.IsValid)
            {
                var service = new ClinicService
                {
                    NameAr = model.NameAr,
                    NameEn = model.NameEn,
                    Type = ServiceType.Service, // Force Type to Service
                    Price = model.Price,
                    Notes = model.Notes,
                    IsActive = true
                };

                _context.ClinicServices.Add(service);
                await _context.SaveChangesAsync();
                return Json(new { success = true, serviceId = service.Id, name = service.NameAr, price = service.Price });
            }

            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            return Json(new { success = false, message = string.Join(", ", errors) });
        }

        // AJAX POST: Services/QuickEdit
        [HttpPost]
        public async Task<IActionResult> QuickEdit([FromBody] ClinicServiceViewModel model)
        {
            if (ModelState.IsValid)
            {
                var service = await _context.ClinicServices.FindAsync(model.Id);
                if (service == null || service.Type != ServiceType.Service) 
                    return Json(new { success = false, message = "لم يتم العثور على الخدمة" });

                service.NameAr = model.NameAr;
                service.Price = model.Price;
                service.Notes = model.Notes;
                service.IsActive = model.IsActive;

                _context.Update(service);
                await _context.SaveChangesAsync();
                return Json(new { success = true });
            }

            var errors = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage);
            return Json(new { success = false, message = string.Join(", ", errors) });
        }

        // GET: Services/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var service = await _context.ClinicServices.FindAsync(id);
            if (service == null || service.Type != ServiceType.Service) return NotFound();

            var model = new ClinicServiceViewModel
            {
                Id = service.Id,
                NameAr = service.NameAr,
                NameEn = service.NameEn,
                Type = service.Type,
                Price = service.Price,
                Notes = service.Notes,
                IsActive = service.IsActive
            };

            return View(model);
        }

        // POST: Services/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ClinicServiceViewModel model)
        {
            if (id != model.Id) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var service = await _context.ClinicServices.FindAsync(id);
                    if (service == null) return NotFound();

                    service.NameAr = model.NameAr;
                    service.NameEn = model.NameEn;
                    service.Price = model.Price;
                    service.Notes = model.Notes;
                    service.IsActive = model.IsActive;

                    _context.Update(service);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ServiceExists(model.Id)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        // AJAX POST: Services/ToggleStatus/5
        [HttpPost]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var service = await _context.ClinicServices.FindAsync(id);
            if (service == null) return Json(new { success = false });

            service.IsActive = !service.IsActive;
            await _context.SaveChangesAsync();
            return Json(new { success = true, isActive = service.IsActive });
        }

        private bool ServiceExists(int id)
        {
            return _context.ClinicServices.Any(e => e.Id == id);
        }
    }
}
