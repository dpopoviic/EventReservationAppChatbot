using Azure.AI.Projects;
using Azure.Identity;
using EventReservationApp.Data;
using EventReservationApp.Models.Entities;
using EventReservationApp.Models.ViewModels;
using EventReservationApp.Services.AgentTools;
using EventReservationApp.Services.Implementations;
using Microsoft.Agents.AI;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;

public class FoundryChatbotConversationService : IChatbotConversationService
{
    // Ported from the portal-authored agent's instructions (see the MCP-based
    // chatbot branch). Adjusted only to drop the file-search-specific guidance,
    // since this agent - built directly in code, see ResolveAgentAsync below -
    // has no file-search resource attached, only the three tools listed here.
    private const string AgentInstructions =
        """
        You are the assistant for the Event Reservation App. You help users discover
        events, check availability, and manage their own reservations.

        AVAILABLE TOOLS AND WHEN TO USE THEM:

        - SearchEvents: use for any "what events are there / upcoming events / events
          in [location/date range]" type request. Supports filtering by search term,
          date range, location, and availability.

        - GetEventAvailability: use to check remaining capacity/spots for one
          specific event the user has already named or that you just found via
          SearchEvents.

        - ManageMyReservations: use this, and ONLY this tool, whenever the user wants
          to list their own reservations, create a new reservation, or cancel an
          existing one. This always acts on behalf of the currently signed-in user -
          never ask the user for their identity, user id, or account details; the
          tool handles that automatically. Confirm what happened using the tool's own
          result (event name, success/failure, reservation id) - never claim a
          reservation succeeded without a successful tool result confirming it.

        RULES:
        - Never invent, guess, or approximate event data, availability numbers, or
          reservation status. If a tool returns no results, say so plainly.
        - If a request falls outside what these tools can answer, reply with exactly
          this and nothing else:
          "Nije moguce izvrsiti trazeni zahtev"
        - Respond in the same language the user writes in (default to Serbian if
          unclear).
        """;

    private readonly ApplicationDbContext _db;
    private readonly ILogger<FoundryChatbotService> _logger;
    private readonly IList<AITool> _tools;

    private readonly Lazy<Task<ChatClientAgent>> _agent;

    public FoundryChatbotConversationService(
        ApplicationDbContext db,
        IConfiguration configuration,
        ILogger<FoundryChatbotService> logger,
        SearchEventsAgentTool searchEventsTool,
        GetEventAvailabilityAgentTool getEventAvailabilityTool,
        ManageMyReservationsAgentTool manageMyReservationsTool)
    {
        _db = db;
        _logger = logger;

        var endpoint =
            configuration["FoundrySettings:ProjectEndpoint"];

        var modelDeploymentName =
            configuration["FoundrySettings:ModelDeploymentName"];

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            throw new InvalidOperationException(
                "FoundrySettings:ProjectEndpoint is not configured.");
        }

        if (string.IsNullOrWhiteSpace(modelDeploymentName))
        {
            throw new InvalidOperationException(
                "FoundrySettings:ModelDeploymentName is not configured.");
        }

        // Built once per scoped instance - AIFunctionFactory.Create reads the
        // [Description] attributes already on these tool methods (ported from
        // the MCP tool surface) to build each function's JSON schema.
        _tools = new List<AITool>
        {
            AIFunctionFactory.Create(searchEventsTool.SearchEvents, name: "SearchEvents"),
            AIFunctionFactory.Create(getEventAvailabilityTool.GetEventAvailability, name: "GetEventAvailability"),
            AIFunctionFactory.Create(manageMyReservationsTool.ManageMyReservations, name: "ManageMyReservations")
        };

        _agent = new Lazy<Task<ChatClientAgent>>(
            () => ResolveAgentAsync(endpoint, modelDeploymentName));
    }

    private static Task<ChatClientAgent> ResolveAgentAsync(
        string endpoint,
        string modelDeploymentName)
    {
        // Builds the agent directly (a "Responses Agent") instead of looking up a
        // portal-authored agent by name. Looking up an existing agent by name -
        // AgentAdministrationClient.GetAgentAsync(agentName) + AsAIAgent(agentRecord) -
        // goes through a code path where the SDK unconditionally discards any
        // per-call tools passed via ChatOptions.Tools (confirmed by decompiling
        // FoundryChatClient.GetAgentEnabledChatOptions: it sets Tools = null
        // whenever an AgentReference is present). Building the agent directly here
        // has no AgentReference, so that code path never runs.
        var projectClient = new AIProjectClient(
            new Uri(endpoint),
            new DefaultAzureCredential());

        var agent = projectClient.AsAIAgent(
            model: modelDeploymentName,
            instructions: AgentInstructions,
            name: "event-reservation-assistant");

        return Task.FromResult(agent);
    }

    public async Task<ChatResponseViewModel> GetReplyAsync(string userId, string message, CancellationToken cancellationToken = default)
    {
        try
        {
            var agent = await _agent.Value;

            var (existingConversation, session) =
                await GetOrCreateSessionAsync(
                    userId,
                    agent,
                    cancellationToken);

            var runOptions = new ChatClientAgentRunOptions
            {
                ChatOptions = new ChatOptions { Tools = _tools }
            };

            var response =
                await agent.RunAsync(
                    message,
                    session,
                    runOptions,
                    cancellationToken);

            await PersistConversationAsync(
                userId,
                existingConversation,
                session,
                cancellationToken);

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

    /// <summary>
    /// Resolves the session to run the next message on: a resumed session bound
    /// to the user's existing conversation id, or - for a first-time user - a
    /// brand new local session. Unlike the previous FoundryAgent-based flow, a
    /// ChatClientAgent session does not get a conversation id from the service
    /// until a message has actually been run on it (CreateSessionCoreAsync just
    /// constructs an empty ChatClientAgentSession locally; the Responses API only
    /// assigns a conversation id once the first RunAsync call completes), so the
    /// id can no longer be minted and persisted up front. See PersistConversationAsync.
    /// </summary>
    private async Task<(ChatConversation? ExistingConversation, AgentSession Session)> GetOrCreateSessionAsync(
        string userId,
        ChatClientAgent agent,
        CancellationToken cancellationToken)
    {
        var existing =
            await _db.ChatConversations
                .SingleOrDefaultAsync(
                    x => x.UserId == userId,
                    cancellationToken);

        var session = existing != null
            ? await agent.CreateSessionAsync(existing.FoundryConversationId, cancellationToken)
            : await agent.CreateSessionAsync(cancellationToken);

        return (existing, session);
    }

    /// <summary>
    /// Persists the conversation id now carried by <paramref name="session"/> -
    /// populated by the framework at the end of the run that just completed -
    /// creating the user's <see cref="ChatConversation"/> row on first use or
    /// refreshing it otherwise.
    /// </summary>
    private async Task PersistConversationAsync(
        string userId,
        ChatConversation? existingConversation,
        AgentSession session,
        CancellationToken cancellationToken)
    {
        if (session is not ChatClientAgentSession chatSession)
        {
            throw new InvalidOperationException("Foundry did not return a ChatClientAgentSession.");
        }

        if (existingConversation != null)
        {
            if (!string.IsNullOrWhiteSpace(chatSession.ConversationId))
            {
                existingConversation.FoundryConversationId = chatSession.ConversationId;
            }

            existingConversation.UpdatedAtUtc = DateTime.UtcNow;

            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        if (string.IsNullOrWhiteSpace(chatSession.ConversationId))
        {
            throw new InvalidOperationException(
                "Foundry ran the conversation but did not return a conversation ID.");
        }

        var conversation = new ChatConversation
        {
            UserId = userId,
            FoundryConversationId = chatSession.ConversationId,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _db.ChatConversations.Add(conversation);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            var alreadyCreated =
                await _db.ChatConversations
                    .SingleOrDefaultAsync(
                        x => x.UserId == userId,
                        cancellationToken);

            if (alreadyCreated is null)
            {
                throw;
            }
        }
    }

    public async Task StartNewConversationAsync(string userId, CancellationToken cancellationToken = default)
    {
        // A ChatClientAgent session only gets a conversation id from the service
        // once a message has been run on it - there is no eager, message-less way
        // to reserve a fresh id up front the way FoundryAgent.CreateConversationSessionAsync
        // used to. So "starting a new conversation" here just forgets the mapping
        // for this user; GetOrCreateSessionAsync naturally starts a brand new
        // session (and PersistConversationAsync records its id) on the next message.
        var existing =
            await _db.ChatConversations
                .SingleOrDefaultAsync(
                    x => x.UserId == userId,
                    cancellationToken);

        if (existing != null)
        {
            _db.ChatConversations.Remove(existing);
            await _db.SaveChangesAsync(cancellationToken);
        }
    }
}
