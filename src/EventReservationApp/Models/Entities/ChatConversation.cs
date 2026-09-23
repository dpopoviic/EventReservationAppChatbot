
public class ChatConversation
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string FoundryConversationId { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; }

    //int TurnCount
}
