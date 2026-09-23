using System.Text.Json;
using System.Text.Json.Serialization;
using EventReservationApp.Models.ViewModels;
using EventReservationApp.Services.Interfaces;

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

    public RasaChatbotConversationService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<RasaChatbotConversationService> logger)
    {
        _logger = logger;

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
