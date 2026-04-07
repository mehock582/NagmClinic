using System.Threading.Tasks;
using NagmClinic.Models;
using NagmClinic.ViewModels;

namespace NagmClinic.Services.Appointments
{
    public interface IAppointmentService
    {
        Task<AppointmentCreateViewModel> BuildCreateViewModelAsync();
        Task<AppointmentCreateViewModel> BuildEditViewModelAsync(int appointmentId);
        
        Task<(bool Success, string Message, int? AppointmentId)> CreateAppointmentAsync(AppointmentCreateViewModel model);
        Task<(bool Success, string Message)> UpdateAppointmentAsync(AppointmentCreateViewModel model);
    }
}
