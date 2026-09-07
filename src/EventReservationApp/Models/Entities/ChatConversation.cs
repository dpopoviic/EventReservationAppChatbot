public class ChatConversation
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string FoundryConversationId { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<ChatMessage> Messages { get; set; } = new List<ChatMessage>(); // da li da bude payload koji je deserialized ili da bude samo lista poruka
}

public class ChatMessage
{
    public int Id { get; set; }
    public int ConversationId { get; set; }
    public string SenderId { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime SentAt { get; set; }
}