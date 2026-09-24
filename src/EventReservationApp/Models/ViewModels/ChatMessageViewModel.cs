namespace EventReservationApp.Models.ViewModels;

public class ChatMessageViewModel
{
    // "user" or "assistant" - matches the CSS classes / badges in Views/Chatbot/Index.cshtml
    public string Sender { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;
}

public class ChatRequestViewModel
{
    public string Message { get; set; } = string.Empty;
}

public class ChatResponseViewModel
{
    public string Reply { get; set; } = string.Empty;
}
