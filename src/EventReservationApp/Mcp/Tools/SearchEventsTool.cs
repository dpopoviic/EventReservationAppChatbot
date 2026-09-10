using System.ComponentModel;
using EventReservationApp.Models.Dtos;
using EventReservationApp.Services.Interfaces;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace EventReservationApp.Mcp.Tools;

/// <summary>
/// MCP tool surface for discovering events. Thin wrapper over
/// <see cref="IEventCatalogService"/> - all filtering/business logic lives
/// in the application service, not here.
/// </summary>
[McpServerToolType]
public sealed class SearchEventsTool
{
    private readonly IEventCatalogService _eventCatalogService;
    private readonly ILogger<SearchEventsTool> _logger;

    public SearchEventsTool(IEventCatalogService eventCatalogService, ILogger<SearchEventsTool> logger)
    {
        _eventCatalogService = eventCatalogService;
        _logger = logger;
    }

    [McpServerTool(Name = "SearchEvents")]
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
        catch (Exception ex) when (ex is not McpException)
        {
            _logger.LogError(ex, "SearchEvents tool call failed.");
            throw new McpException("Unable to search events right now. Please try again later.");
        }
    }
}
