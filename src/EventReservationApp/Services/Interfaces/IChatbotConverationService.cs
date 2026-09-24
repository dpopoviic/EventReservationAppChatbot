using EventReservationApp.Models.ViewModels;

public interface IChatbotConversationService
{
    Task<ChatResponseViewModel> GetReplyAsync(
        string userId,
        string message,
        CancellationToken cancellationToken = default);

    // Latest user/bot messages of the current conversation, oldest first.
    Task<IReadOnlyList<ChatMessageViewModel>> GetRecentMessagesAsync(
        string userId,
        int count,
        CancellationToken cancellationToken = default);

    Task StartNewConversationAsync(
        string userId,
        CancellationToken cancellationToken = default);
}