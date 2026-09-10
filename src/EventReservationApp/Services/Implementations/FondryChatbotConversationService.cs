using Azure.AI.Projects;
using Azure.Identity;
using EventReservationApp.Data;
using EventReservationApp.Models.ViewModels;
using EventReservationApp.Services.Implementations;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Foundry;
using Microsoft.EntityFrameworkCore;

public class FoundryChatbotConversationService : IChatbotConversationService
{ 
    private readonly ApplicationDbContext _db;
    private readonly ILogger<FoundryChatbotService> _logger;

    private readonly Lazy<Task<FoundryAgent>> _agent;

    public FoundryChatbotConversationService(
        ApplicationDbContext db,
        IConfiguration configuration,
        ILogger<FoundryChatbotService> logger)
    {
        _db = db;
        _logger = logger;

        var endpoint =
            configuration["FoundrySettings:ProjectEndpoint"];

        var agentName =
            configuration["FoundrySettings:AgentName"];

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException(
                "FoundrySettings:ProjectEndpoint is not configured.");
        }

        if (string.IsNullOrWhiteSpace(agentName))
        {
            throw new InvalidOperationException(
                "FoundrySettings:AgentName is not configured.");
        }

        _agent = new Lazy<Task<FoundryAgent>>(
            () => ResolveAgentAsync(endpoint, agentName));
    }
      private static async Task<FoundryAgent> ResolveAgentAsync(
        string endpoint,
        string agentName)
    {
        var projectClient = new AIProjectClient(
            new Uri(endpoint),
            new DefaultAzureCredential());

        var agentRecord =
            await projectClient.AgentAdministrationClient
                .GetAgentAsync(agentName);

        return projectClient.AsAIAgent(agentRecord);
    }


    public async Task<ChatResponseViewModel> GetReplyAsync(string userId, string message, CancellationToken cancellationToken = default)
    {
        try
        {
            var agent = await _agent.Value;

            var conversation =
                await GetOrCreateConversationAsync(
                    userId,
                    agent,
                    cancellationToken);

            var session =
                await agent.CreateSessionAsync(
                    conversation.FoundryConversationId,
                    cancellationToken);

            var response =
                await agent.RunAsync(
                    message,
                    session);

            conversation.UpdatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);

           var reply = response?.ToString();

           return new ChatResponseViewModel { 
                Reply = string.IsNullOrWhiteSpace(reply) 
                ? "Žao mi je, nisam siguran kako da odgovorim na to. Možete li pokušati drugačije?" 
                : reply 
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Foundry chatbot call failed for user {UserId}.",
                userId);

            return new ChatResponseViewModel
            {
                Reply =
                    "Došlo je do problema prilikom komunikacije sa chatbotom. " +
                    "Molimo pokušajte ponovo kasnije."
            };
        }
    }
    private async Task<ChatConversation> GetOrCreateConversationAsync(string userId, FoundryAgent agent, CancellationToken cancellationToken)
    {
        var existing =
            await _db.ChatConversations
                .SingleOrDefaultAsync(
                    x => x.UserId == userId,
                    cancellationToken);

        if (existing != null)
        {
            return existing;
        }

        var session =
            await agent.CreateConversationSessionAsync(
                cancellationToken);

        if (session is not ChatClientAgentSession chatSession) 
        { 
            throw new InvalidOperationException( "Foundry did not return a ChatClientAgentSession."); 
        }

        var conversationId = chatSession.ConversationId;

        if (string.IsNullOrWhiteSpace(conversationId)) 
        { 
            throw new InvalidOperationException( "Foundry created a conversation but did not return a conversation ID."); 
        }
        var conversation = new ChatConversation
        {
            UserId = userId,
            FoundryConversationId = conversationId,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _db.ChatConversations.Add(conversation);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);

            return conversation;
        }
        catch (DbUpdateException)
        {
            var alreadyCreated =
                await _db.ChatConversations
                    .SingleOrDefaultAsync(
                        x => x.UserId == userId,
                        cancellationToken);
            if (alreadyCreated != null) 
            { 
                return alreadyCreated; 
            } 
            
            throw;
        }
    }
    public async Task StartNewConversationAsync(string userId, CancellationToken cancellationToken = default)
    {
         var agent = await _agent.Value;

        var session =
            await agent.CreateConversationSessionAsync(
                cancellationToken);

        if (session is not ChatClientAgentSession chatSession) 
        { 
            throw new InvalidOperationException( "Foundry did not return a ChatClientAgentSession."); 
        }

        var conversationId = chatSession.ConversationId;

        if (string.IsNullOrWhiteSpace(conversationId)) 
        { 
            throw new InvalidOperationException( "Foundry created a conversation but did not return a conversation ID."); 
        }

        var existing =
            await _db.ChatConversations
                .SingleOrDefaultAsync(
                    x => x.UserId == userId,
                    cancellationToken);
        if (existing == null) 
        { 
            var conversation = new ChatConversation 
            { 
                UserId = userId, 
                FoundryConversationId = conversationId, 
                CreatedAtUtc = DateTime.UtcNow, 
                UpdatedAtUtc = DateTime.UtcNow 
            };
             _db.ChatConversations.Add(conversation); 
        }
        
        else
        {
            existing.FoundryConversationId = conversationId;
            existing.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}