using Microsoft.AspNetCore.Identity;
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

namespace NagmClinic
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(connectionString));
            builder.Services.AddDatabaseDeveloperPageExceptionFilter();

            builder.Services.AddDefaultIdentity<IdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
                .AddEntityFrameworkStores<ApplicationDbContext>();
            builder.Services.AddControllersWithViews();
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

            var app = builder.Build();

            // Configure the HTTP request pipeline.
            if (app.Environment.IsDevelopment())
            {
                app.UseMigrationsEndPoint();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");
            app.MapRazorPages();

            using (var scope = app.Services.CreateScope())
            {
                var labCatalogSeedService = scope.ServiceProvider.GetRequiredService<ILabCatalogSeedService>();
                var labDeviceMappingSeedService = scope.ServiceProvider.GetRequiredService<ILabDeviceMappingSeedService>();
                labCatalogSeedService.SeedDefaultsAsync().GetAwaiter().GetResult();
                labDeviceMappingSeedService.EnsureMappingsAsync().GetAwaiter().GetResult();
            }

            app.Run();
        }
    }
}
