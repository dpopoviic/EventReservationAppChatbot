using System.Collections.Concurrent;
using Azure.AI.Projects;
using Azure.Identity;
using EventReservationApp.Models.ViewModels;
using EventReservationApp.Services.Interfaces;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Foundry;

namespace EventReservationApp.Services.Implementations;

public class FoundryChatbotService : IChatbotService
{
    private readonly Lazy<Task<FoundryAgent>> _agent;
    private readonly ConcurrentDictionary<string, Task<AgentSession>> _sessionsByUser = new();
    private readonly ILogger<FoundryChatbotService> _logger;

    public FoundryChatbotService(IConfiguration configuration, ILogger<FoundryChatbotService> logger)
    {
        _logger = logger;

        var endpoint = configuration["FoundrySettings:ProjectEndpoint"];
        var agentName = configuration["FoundrySettings:AgentName"];

        if (string.IsNullOrWhiteSpace(endpoint) || endpoint.Contains("YOUR_FOUNDRY"))
        {
            throw new InvalidOperationException(
                "FoundrySettings:ProjectEndpoint is not set. Open appsettings.json and paste in your ");
        }

        if (string.IsNullOrWhiteSpace(agentName) || agentName.Contains("YOUR_FOUNDRY"))
        {
            throw new InvalidOperationException(
                "FoundrySettings:AgentName is not set. Open appsettings.json and paste in the name of the " +
                "agent you created in the Foundry portal");
        }

        _agent = new Lazy<Task<FoundryAgent>>(() => ResolveAgentAsync(endpoint, agentName));
    }

    private static async Task<FoundryAgent> ResolveAgentAsync(string endpoint, string agentName)
    {
        var projectClient = new AIProjectClient(new Uri(endpoint), new DefaultAzureCredential());
        var agentRecord = await projectClient.AgentAdministrationClient.GetAgentAsync(agentName);
        return projectClient.AsAIAgent(agentRecord);
    }

    public async Task<ChatResponseViewModel> GetReplyAsync(string userId, string message)
    {
        try
        {
            var agent = await _agent.Value;

            var session = await _sessionsByUser.GetOrAdd(userId, _ => agent.CreateSessionAsync().AsTask());

            var response = await agent.RunAsync(message, session);
            var reply = response?.ToString();

            return new ChatResponseViewModel
            {
                Reply = string.IsNullOrWhiteSpace(reply)
                    ? "Žao mi je, nisam siguran kako da odgovorim na to. Možete li pokužati drugačije?"
                    : reply
            };
        }
        catch (Exception ex)
        {
          
            _logger.LogError(ex, "Foundry chatbot call failed for user {UserId}.", userId);
            return new ChatResponseViewModel
            {
                Reply = "Došlo je do problema prilikom komunikacije s chatbotom. Molimo pokušajte ponovo kasnije."
            };
        }
    }
}
