using EventReservationApp.Models.ViewModels;

namespace EventReservationApp.Services.Interfaces;

public interface IChatbotService
{
    Task<ChatResponseViewModel> GetReplyAsync(string userId, string message);
}
