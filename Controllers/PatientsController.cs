using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NagmClinic.Data;
using NagmClinic.Models;
using NagmClinic.Extensions;
using NagmClinic.Models.DataTables;
using NagmClinic.Services.Patients;

namespace NagmClinic.Controllers
{
    public class PatientsController : Controller
    {
        private readonly IPatientService _patientService;

        public PatientsController(IPatientService patientService)
        {
            _patientService = patientService;
        }

        // GET: Patients
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> GetPatientsData()
        {
            try
            {
                var dtParams = Request.GetDataTablesParameters();
                var response = await _patientService.GetPatientsDataAsync(dtParams);
                return Json(response);
            }
            catch (Exception ex)
            {
                return Json(new DataTablesResponse<object> { error = ex.Message });
            }
        }

        // GET: Patients/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var patient = await _patientService.GetPatientDetailsAsync(id.Value);

            if (patient == null) return NotFound();

            return View(patient);
        }

        // GET: Patients/Create
        public IActionResult Create()
        {
            return View(new Patient { Age = 25 });
        }

        // POST: Patients/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("FullName,PhoneNumber,Gender,Age,Address")] Patient patient)
        {
            if (ModelState.IsValid)
            {
                var result = await _patientService.CreatePatientAsync(patient);
                if (result.Success)
                {
                    TempData["SuccessMessage"] = result.Message;
                    return RedirectToAction(nameof(Index));
                }
                
                ModelState.AddModelError("PhoneNumber", result.Message);
            }
            return View(patient);
        }

        // GET: Patients/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var patient = await _patientService.GetPatientByIdAsync(id.Value);
            if (patient == null) return NotFound();
            return View(patient);
        }

        // POST: Patients/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("Id,FullName,PhoneNumber,Gender,Age,Address,IsActive,CreatedAt")] Patient patient)
        {
            if (id != patient.Id) return NotFound();

            if (ModelState.IsValid)
            {
                var result = await _patientService.UpdatePatientAsync(patient);
                if (result.Success)
                {
                    TempData["SuccessMessage"] = result.Message;
                    return RedirectToAction(nameof(Index));
                }

                ModelState.AddModelError("PhoneNumber", result.Message);
            }
            return View(patient);
        }

        // AJAX: Search Patients
        [HttpGet]
        public async Task<IActionResult> Search(string query)
        {
            var results = await _patientService.SearchPatientsAsync(query);
            return Json(results);
        }

        // AJAX: Quick Create Patient
        [HttpPost]
        public async Task<IActionResult> QuickCreate(string fullName, string phoneNumber, string genderString, string address, int age)
        {
            try 
            {
                var result = await _patientService.QuickCreatePatientAsync(fullName, phoneNumber, genderString, address, age);
                if (result.Success)
                {
                    return Json(new { success = true, patientId = result.Data!.Id, fullName = result.Data.FullName });
                }
                return Json(new { success = false, message = result.Message });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "حدث خطأ داخلي: " + ex.Message });
            }
        }

        // AJAX: Quick Edit Patient
        [HttpPost]
        public async Task<IActionResult> QuickEdit(int id, string fullName, string phoneNumber, string genderString, string address, int age, bool isActive)
        {
            try 
            {
                var result = await _patientService.QuickEditPatientAsync(id, fullName, phoneNumber, genderString, address, age, isActive);
                if (result.Success)
                {
                    return Json(new { success = true });
                }
                return Json(new { success = false, message = result.Message });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "حدث خطأ داخلي: " + ex.Message });
            }
        }
    }
}
