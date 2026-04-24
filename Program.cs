using Serilog;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using NagmClinic.Data;
using NagmClinic.Models.Configuration;
using NagmClinic.Services.Pharmacy;
using NagmClinic.Services.Appointments;
using NagmClinic.Services.Patients;
using NagmClinic.Services.Branding;
using NagmClinic.Services.Laboratory;
using NagmClinic.Services.Laboratory.Connector;
using NagmClinic.Services.Reports;
using NagmClinic.Localization;
namespace NagmClinic
{
    public partial class Program
    {
        private static string _startupErrorMessage = "No errors detected.";

        public static void Main(string[] args)
        {
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Error()
                .Enrich.FromLogContext()
                .WriteTo.File("Logs/erp-error-log-.txt", rollingInterval: RollingInterval.Day)
                .CreateLogger();

            try
            {
                var builder = WebApplication.CreateBuilder(args);
                builder.Host.UseSerilog();

                // Add services to the container.
                var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
                builder.Services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseSqlServer(connectionString, sqlServerOptions => sqlServerOptions.EnableRetryOnFailure()));
                builder.Services.AddDatabaseDeveloperPageExceptionFilter();

                builder.Services.AddDefaultIdentity<IdentityUser>(options => {
                    options.SignIn.RequireConfirmedAccount = false;
                    options.Lockout.AllowedForNewUsers = true;
                    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromDays(365 * 100); // 100 years lockout
                    options.Lockout.MaxFailedAccessAttempts = 5;
                })
                    .AddRoles<IdentityRole>()
                    .AddEntityFrameworkStores<ApplicationDbContext>()
                    .AddErrorDescriber<ArabicIdentityErrorDescriber>();
                builder.Services.AddControllersWithViews(options =>
                {
                    var policy = new AuthorizationPolicyBuilder()
                        .RequireAuthenticatedUser()
                        .Build();
                    options.Filters.Add(new AuthorizeFilter(policy));
                });
                builder.Services.AddHttpContextAccessor();
                builder.Services.AddScoped<IPharmacyMasterDataService, PharmacyMasterDataService>();
                builder.Services.AddScoped<IPharmacyStockService, PharmacyStockService>();
                builder.Services.AddScoped<IAppointmentService, AppointmentService>();
                builder.Services.AddScoped<IPatientService, PatientService>();
                builder.Services.AddScoped<IPharmacySalesService, PharmacySalesService>();
                builder.Services.AddScoped<IPharmacyPurchasesService, PharmacyPurchasesService>();
                builder.Services.AddScoped<ILabCatalogSeedService, LabCatalogSeedService>();
                builder.Services.AddScoped<ILabResultImportService, LabResultImportService>();
                builder.Services.AddScoped<ILabPatientMatchResolver, LabPatientMatchResolver>();
                builder.Services.AddScoped<ILabDeviceTestMappingService, LabDeviceTestMappingService>();
                builder.Services.AddScoped<ILabDeviceMappingSeedService, LabDeviceMappingSeedService>();
                builder.Services.AddSingleton<IDeviceDataChannelFactory, DeviceDataChannelFactory>();
                builder.Services.AddTransient<IDeviceConnector, ECSeriesConnector>();
                builder.Services.AddTransient<IDeviceConnector, LansionbioConnector>();
                builder.Services.AddTransient<IDeviceConnector, BioelabConnector>();
                builder.Services.Configure<ConnectorDispatchOptions>(builder.Configuration.GetSection("LabConnectorDispatch"));
                builder.Services.Configure<ConnectorClinicApiOptions>(builder.Configuration.GetSection("LabConnectorClient"));
                builder.Services.AddSingleton<IConnectorOutboxStore, JsonFileConnectorOutboxStore>();
                builder.Services.AddHttpClient<IConnectorClinicApiClient, ConnectorClinicApiClient>();
                builder.Services.AddScoped<ConnectorResultDispatchService>();
                builder.Services.AddScoped<IConnectorIngestionPipeline, ConnectorIngestionPipeline>();
                builder.Services.Configure<LabConnectorApiOptions>(builder.Configuration.GetSection(LabConnectorApiOptions.SectionName));
                
                builder.Services.AddSingleton<HeartbeatStore>();
                builder.Services.AddHostedService<HeartbeatMonitorService>();

                builder.Services.Configure<ClinicBrandingOptions>(builder.Configuration.GetSection("ClinicBranding"));
                builder.Services.AddSingleton<IClinicBrandingService, ClinicBrandingService>();
                builder.Services.AddScoped<IQrCodeService, QrCodeService>();

                // Wire the 403 redirect to our Arabic AccessDenied page
                builder.Services.ConfigureApplicationCookie(options =>
                {
                    options.LoginPath = "/Identity/Account/Login";
                    options.LogoutPath = "/Identity/Account/Logout";
                    options.AccessDeniedPath = "/Account/AccessDenied";
                });

                var app = builder.Build();

                // Configure the HTTP request pipeline.
                if (app.Environment.IsDevelopment())
                {
                    app.UseMigrationsEndPoint();
                }
                else
                {
                    app.UseExceptionHandler("/Home/Error");
                    app.UseHsts();
                }

                // Note: HTTPS redirection removed — the hosting reverse proxy handles SSL termination
                app.UseStaticFiles();

                app.UseRouting();

                app.UseAuthentication();
                app.UseAuthorization();

                app.MapControllerRoute(
                    name: "default",
                    pattern: "{controller=Home}/{action=Index}/{id?}");
                app.MapRazorPages();

                using (var scope = app.Services.CreateScope())
                {
                    try
                    {
                        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        if (app.Environment.IsProduction())
                        {
                            context.Database.Migrate();
                        }

                        var labCatalogSeedService = scope.ServiceProvider.GetRequiredService<ILabCatalogSeedService>();
                        var labDeviceMappingSeedService = scope.ServiceProvider.GetRequiredService<ILabDeviceMappingSeedService>();
                        labCatalogSeedService.SeedDefaultsAsync().GetAwaiter().GetResult();
                        labDeviceMappingSeedService.EnsureMappingsAsync().GetAwaiter().GetResult();

                        // ── Seed Roles & Default Admin ──────────────────────────
                        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
                        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

                        string[] roles = { "Admin", "Doctor", "LabTech", "Cashier", "Pharmacist" };
                        foreach (var role in roles)
                        {
                            if (!roleManager.RoleExistsAsync(role).GetAwaiter().GetResult())
                                roleManager.CreateAsync(new IdentityRole(role)).GetAwaiter().GetResult();
                        }

                        // Seed a default admin only when no admin exists yet
                        const string adminEmail = "admin@clinic.local";
                        const string adminPassword = "Admin@123!";
                        var existingAdmin = userManager.FindByEmailAsync(adminEmail).GetAwaiter().GetResult();
                        if (existingAdmin == null)
                        {
                            var adminUser = new IdentityUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
                            var created = userManager.CreateAsync(adminUser, adminPassword).GetAwaiter().GetResult();
                            if (created.Succeeded)
                                userManager.AddToRoleAsync(adminUser, "Admin").GetAwaiter().GetResult();
                        }
                    }
                    catch (Exception ex)
                    {
                        _startupErrorMessage = $"Database Startup Error: {ex.Message} | Inner: {ex.InnerException?.Message}";
                        Log.Error(ex, "Failed to migrate or seed database on startup.");
                    }
                }

                app.MapGet("/db-status", () => Results.Ok(new { Status = _startupErrorMessage }));
                app.Run();
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Host terminated unexpectedly during startup.");

                _startupErrorMessage = $"FATAL Startup Crash: {ex.Message} | Inner: {ex.InnerException?.Message} | Stack: {ex.StackTrace?.Substring(0, Math.Min(500, ex.StackTrace?.Length ?? 0))}";

                var fallbackBuilder = WebApplication.CreateBuilder(args);
                var fallbackApp = fallbackBuilder.Build();
                fallbackApp.MapGet("/", () => Results.Text(_startupErrorMessage, "text/plain"));
                fallbackApp.MapGet("/db-status", () => Results.Text(_startupErrorMessage, "text/plain"));
                fallbackApp.Run();
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }

    public partial class Program { }
}

