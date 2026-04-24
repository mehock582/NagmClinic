using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NagmClinic.Models;
using NagmClinic.ViewModels;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using NagmClinic.Data;

namespace NagmClinic.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly ApplicationDbContext _context;

        public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetDashboardStats()
        {
            try
            {
                var today = DateTime.Today;
                var tomorrow = today.AddDays(1);
                var startOfMonth = new DateTime(today.Year, today.Month, 1);

                int totalPatients = await _context.Patients.CountAsync();
                
                // Appointments for today (Safely checking range rather than .Date)
                int appointmentsToday = await _context.Appointments
                    .Where(a => a.AppointmentDate >= today && a.AppointmentDate < tomorrow && a.Status == NagmClinic.Models.Enums.AppointmentStatus.Confirmed)
                    .CountAsync();

                // Monthly revenue: safely split into two simple sums to guarantee SQL translation
                // Crucially, cast to (decimal?) inside SumAsync to prevent EF crashes when the table/month is completely empty and SQL returns NULL.
                decimal consultationFees = await _context.Appointments
                    .Where(a => a.AppointmentDate >= startOfMonth && a.Status == NagmClinic.Models.Enums.AppointmentStatus.Confirmed)
                    .SumAsync(a => (decimal?)a.ConsultationFee) ?? 0m;

                decimal itemsTotal = await _context.AppointmentItems
                    .Where(i => i.Appointment != null && i.Appointment.AppointmentDate >= startOfMonth && i.Appointment.Status == NagmClinic.Models.Enums.AppointmentStatus.Confirmed)
                    .SumAsync(i => (decimal?)i.TotalPrice) ?? 0m;

                decimal monthlyRevenue = consultationFees + itemsTotal;

                return Json(new {
                    success = true,
                    totalPatients = totalPatients.ToString("N0"),
                    appointmentsToday = appointmentsToday,
                    monthlyRevenue = monthlyRevenue.ToString("N2")
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching dashboard stats");
                return Json(new {
                    success = false,
                    totalPatients = "--",
                    appointmentsToday = "--",
                    monthlyRevenue = "0.00"
                });
            }
        }

        [AllowAnonymous]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
