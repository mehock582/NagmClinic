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
                var sevenDaysAgo = today.AddDays(-6);

                int totalPatients = await _context.Patients.CountAsync();
                
                int appointmentsToday = await _context.Appointments
                    .Where(a => a.AppointmentDate >= today && a.AppointmentDate < tomorrow && a.Status == NagmClinic.Models.Enums.AppointmentStatus.Confirmed)
                    .CountAsync();

                // Revenue calculation
                decimal consultationFees = await _context.Appointments
                    .Where(a => a.AppointmentDate >= startOfMonth && a.Status == NagmClinic.Models.Enums.AppointmentStatus.Confirmed)
                    .SumAsync(a => (decimal?)a.ConsultationFee) ?? 0m;

                decimal itemsTotal = await _context.AppointmentItems
                    .Where(i => i.Appointment != null && i.Appointment.AppointmentDate >= startOfMonth && i.Appointment.Status == NagmClinic.Models.Enums.AppointmentStatus.Confirmed)
                    .SumAsync(i => (decimal?)i.TotalPrice) ?? 0m;

                // Add Pharmacy Sales to Monthly Revenue
                decimal pharmacySalesTotal = await _context.PharmacySales
                    .Where(s => s.SaleDate >= startOfMonth)
                    .SumAsync(s => (decimal?)s.TotalAmount) ?? 0m;

                decimal monthlyRevenue = consultationFees + itemsTotal + pharmacySalesTotal;

                // Pharmacy specific stats
                int lowStockCount = await _context.PharmacyItems
                    .Where(i => i.IsActive && i.Batches.Where(b => b.QuantityRemaining > 0 && b.ExpiryDate >= today).Sum(b => (decimal?)b.QuantityRemaining) <= i.ReorderLevel)
                    .CountAsync();

                int totalPharmacyItems = await _context.PharmacyItems.CountAsync(i => i.IsActive);

                // Revenue Trend (Last 7 Days)
                var trendDays = Enumerable.Range(0, 7).Select(i => sevenDaysAgo.AddDays(i)).ToList();
                var trendData = new List<decimal>();
                var trendLabels = new List<string>();

                foreach(var day in trendDays)
                {
                    var nextDay = day.AddDays(1);
                    var daySales = await _context.PharmacySales
                        .Where(s => s.SaleDate >= day && s.SaleDate < nextDay)
                        .SumAsync(s => (decimal?)s.TotalAmount) ?? 0m;
                    
                    var dayAppts = await _context.Appointments
                        .Where(a => a.AppointmentDate >= day && a.AppointmentDate < nextDay && a.Status == NagmClinic.Models.Enums.AppointmentStatus.Confirmed)
                        .SumAsync(a => (decimal?)a.ConsultationFee) ?? 0m;

                    trendData.Add(daySales + dayAppts);
                    trendLabels.Add(day.ToString("MM/dd"));
                }

                // Appointment Status Breakdown (Donut Chart)
                var statusBreakdown = await _context.Appointments
                    .Where(a => a.AppointmentDate >= today && a.AppointmentDate < tomorrow)
                    .GroupBy(a => a.Status)
                    .Select(g => new { Status = g.Key.ToString(), Count = g.Count() })
                    .ToListAsync();

                return Json(new {
                    success = true,
                    totalPatients = totalPatients.ToString("N0"),
                    appointmentsToday = appointmentsToday,
                    monthlyRevenue = monthlyRevenue.ToString("N0"),
                    lowStockCount = lowStockCount,
                    totalPharmacyItems = totalPharmacyItems,
                    trendLabels = trendLabels,
                    trendData = trendData,
                    statusLabels = statusBreakdown.Select(s => s.Status).ToList(),
                    statusData = statusBreakdown.Select(s => s.Count).ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching dashboard stats");
                return Json(new {
                    success = false,
                    totalPatients = "--",
                    appointmentsToday = "--",
                    monthlyRevenue = "0",
                    lowStockCount = 0,
                    totalPharmacyItems = 0,
                    trendLabels = new List<string>(),
                    trendData = new List<decimal>()
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
