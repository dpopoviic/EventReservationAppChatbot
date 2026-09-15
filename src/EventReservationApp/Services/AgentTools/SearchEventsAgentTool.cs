using System.ComponentModel;
using EventReservationApp.Models.Dtos;
using EventReservationApp.Services.Interfaces;

namespace EventReservationApp.Services.AgentTools;

/// <summary>
/// Agent tool surface for discovering events. Thin wrapper over
/// <see cref="IEventCatalogService"/> - all filtering/business logic lives
/// in the application service, not here. This is a plain class with no MCP
/// attributes; its schema is built by AIFunctionFactory.Create from the
/// [Description] attributes below.
/// </summary>
public sealed class SearchEventsAgentTool
{
    private readonly IEventCatalogService _eventCatalogService;
    private readonly ILogger<SearchEventsAgentTool> _logger;

    public SearchEventsAgentTool(IEventCatalogService eventCatalogService, ILogger<SearchEventsAgentTool> logger)
    {
        _eventCatalogService = eventCatalogService;
        _logger = logger;
    }

    [Description(
        "Searches events using the filters provided. Use this when the user asks which events are " +
        "available or wants to discover events. Returns enough detail about each matching event " +
        "(name, description, location, dates, capacity and availability) that a follow-up lookup is " +
        "usually unnecessary.")]
    public async Task<IReadOnlyList<EventSearchResultDto>> SearchEvents(
        [Description("Free-text term matched against the event name and description. Omit to match all events.")]
        string? searchTerm = null,
        [Description("Only include events starting on or after this date (ISO 8601, e.g. 2026-10-01).")]
        DateTime? startDate = null,
        [Description("Only include events starting on or before this date (ISO 8601, e.g. 2026-10-31).")]
        DateTime? endDate = null,
        [Description("Only include events whose location contains this text (partial match).")]
        string? location = null,
        [Description("If true, only include events that still have at least one available place.")]
        bool? availability = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var filter = new EventSearchFilter
            {
                SearchTerm = searchTerm,
                StartDateFrom = startDate,
                StartDateTo = endDate,
                Location = location,
                OnlyAvailable = availability
            };

            return await _eventCatalogService.SearchEventsAsync(filter, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SearchEvents agent tool call failed.");
            throw;
        }
    }
}
