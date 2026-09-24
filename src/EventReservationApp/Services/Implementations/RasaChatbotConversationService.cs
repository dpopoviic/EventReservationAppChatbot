using System.Text.Json;
using System.Text.Json.Serialization;
using EventReservationApp.Models.ViewModels;
using EventReservationApp.Services.Interfaces;
using Microsoft.Data.SqlClient;

namespace EventReservationApp.Services.Implementations;


public class RasaChatbotConversationService : IChatbotConversationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<RasaChatbotConversationService> _logger;
    private readonly string _tokenQuery;
    private readonly string? _trackerConnectionString;

    public RasaChatbotConversationService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<RasaChatbotConversationService> logger)
    {
        _logger = logger;

        // Rasa's SQLTrackerStore database (see tracker_store in the Rasa endpoints.yml).
        // Read-only here; Rasa owns the schema.
        _trackerConnectionString = configuration.GetConnectionString("RasaTracker");

        var baseUrl = configuration["RasaSettings:BaseUrl"];
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("RasaSettings:BaseUrl is not configured.");
        }

        httpClient.BaseAddress = new Uri(baseUrl);

        
        var authToken = configuration["RasaSettings:AuthToken"];
        _tokenQuery = string.IsNullOrWhiteSpace(authToken)
            ? string.Empty
            : $"?token={Uri.EscapeDataString(authToken)}";

        _httpClient = httpClient;
    }

    public async Task<ChatResponseViewModel> GetReplyAsync(
        string userId,
        string message,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var payload = new RasaWebhookRequest
            {
                Sender = userId,
                Message = message,
                Metadata = new RasaMessageMetadata { UserId = userId }
            };

            using var response = await _httpClient.PostAsJsonAsync(
                $"webhooks/rest/webhook{_tokenQuery}", payload, JsonOptions, cancellationToken);

            response.EnsureSuccessStatusCode();

            var botMessages = await response.Content.ReadFromJsonAsync<List<RasaBotMessage>>(
                JsonOptions, cancellationToken) ?? new List<RasaBotMessage>();

            var reply = string.Join(
                "\n\n",
                botMessages
                    .Select(m => m.Text)
                    .Where(text => !string.IsNullOrWhiteSpace(text)));

            return new ChatResponseViewModel
            {
                Reply = string.IsNullOrWhiteSpace(reply)
                    ? "Žao mi je, nisam siguran kako da odgovorim na to. Možete li pokušati drugačije?"
                    : reply
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rasa chatbot call failed for user {UserId}.", userId);
            return new ChatResponseViewModel
            {
                Reply = "Došlo je do problema prilikom komunikacije sa chatbotom. " +
                        "Molimo pokušajte ponovo kasnije."
            };
        }
    }

    public async Task<IReadOnlyList<ChatMessageViewModel>> GetRecentMessagesAsync(
        string userId,
        int count,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_trackerConnectionString))
        {
            _logger.LogWarning("ConnectionStrings:RasaTracker is not configured; chat history is disabled.");
            return Array.Empty<ChatMessageViewModel>();
        }

        // sender_id is the Identity user id (see GetReplyAsync). Only messages after
        // the latest "restart" event are returned, so "new conversation" starts empty.
        const string sql = """
            SELECT TOP (@count) type_name, timestamp, data
            FROM events
            WHERE sender_id = @senderId
              AND type_name IN ('user', 'bot')
              AND id > COALESCE(
                  (SELECT MAX(id) FROM events
                   WHERE sender_id = @senderId AND type_name = 'restart'), 0)
            ORDER BY id DESC
            """;

        try
        {
            await using var connection = new SqlConnection(_trackerConnectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand(sql, connection);
            command.Parameters.Add("@count", System.Data.SqlDbType.Int).Value = count;
            command.Parameters.Add("@senderId", System.Data.SqlDbType.NVarChar, 255).Value = userId;

            var messages = new List<ChatMessageViewModel>();

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var text = ReadText(reader.IsDBNull(2) ? null : reader.GetString(2));
                if (string.IsNullOrWhiteSpace(text))
                {
                    continue;
                }

                messages.Add(new ChatMessageViewModel
                {
                    Sender = reader.GetString(0) == "user" ? "user" : "assistant",
                    Text = text,
                    Timestamp = reader.IsDBNull(1)
                        ? DateTime.UtcNow
                        : DateTimeOffset.FromUnixTimeMilliseconds(
                            (long)(Convert.ToDouble(reader.GetValue(1)) * 1000)).UtcDateTime
                });
            }

            // Query is newest-first so TOP picks the latest; the UI wants oldest-first.
            messages.Reverse();
            return messages;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load chat history for user {UserId}.", userId);
            return Array.Empty<ChatMessageViewModel>();
        }
    }

    // The data column holds the serialized Rasa event; user and bot events keep the message in "text".
    private static string? ReadText(string? eventJson)
    {
        if (string.IsNullOrWhiteSpace(eventJson))
        {
            return null;
        }

        using var document = JsonDocument.Parse(eventJson);
        return document.RootElement.TryGetProperty("text", out var text) &&
               text.ValueKind == JsonValueKind.String
            ? text.GetString()
            : null;
    }

    public async Task StartNewConversationAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var restartEvent = new { @event = "restart" };

            using var response = await _httpClient.PostAsJsonAsync(
                $"conversations/{Uri.EscapeDataString(userId)}/tracker/events{_tokenQuery}",
                restartEvent,
                JsonOptions,
                cancellationToken);

            response.EnsureSuccessStatusCode();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restart Rasa tracker for user {UserId}.", userId);
            throw;
        }
    }

    private sealed class RasaWebhookRequest
    {
        public required string Sender { get; init; }
        public required string Message { get; init; }
        public RasaMessageMetadata? Metadata { get; init; }
    }

    private sealed class RasaMessageMetadata
    {
        [JsonPropertyName("user_id")]
        public required string UserId { get; init; }
    }

    private sealed class RasaBotMessage
    {
        public string? Text { get; init; }
    }
}
