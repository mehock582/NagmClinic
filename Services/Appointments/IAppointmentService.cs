using System.Threading.Tasks;
using NagmClinic.Models;
using NagmClinic.ViewModels;
using NagmClinic.Models.DataTables;

namespace NagmClinic.Services.Appointments
{
    public interface IAppointmentService
    {
        Task<AppointmentCreateViewModel> BuildCreateViewModelAsync();
        Task<AppointmentCreateViewModel?> BuildEditViewModelAsync(int appointmentId);
        Task<DataTablesResponse<object>> GetAppointmentsDataTableAsync(DataTablesParameters dtParams);
        
        Task<(bool Success, string Message, int? AppointmentId)> CreateAppointmentAsync(AppointmentCreateViewModel model);
        Task<(bool Success, string Message)> UpdateAppointmentAsync(AppointmentCreateViewModel model);
        Task<(bool Success, string Message)> RefundAppointmentItemAsync(int itemId);
    }
}
