using EventReservationApp.Models.ViewModels;

public interface IChatbotConversationService
{
    Task<ChatResponseViewModel> GetReplyAsync(
        string userId,
        string message,
        CancellationToken cancellationToken = default);

    Task StartNewConversationAsync(
        string userId,
        CancellationToken cancellationToken = default);
}