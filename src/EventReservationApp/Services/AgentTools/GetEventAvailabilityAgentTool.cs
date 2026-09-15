using System.ComponentModel;
using EventReservationApp.Models.Dtos;
using EventReservationApp.Services.Interfaces;

namespace EventReservationApp.Services.AgentTools;

/// <summary>
/// Agent tool surface for checking a single event's reservation availability.
/// Thin wrapper over <see cref="IEventCatalogService"/> - capacity/availability
/// rules live entirely in the application service. Plain class, no MCP
/// attributes; [Description] attributes are read by AIFunctionFactory.Create
/// to build the tool's schema.
/// </summary>
public sealed class GetEventAvailabilityAgentTool
{
    private readonly IEventCatalogService _eventCatalogService;
    private readonly ILogger<GetEventAvailabilityAgentTool> _logger;

    public GetEventAvailabilityAgentTool(IEventCatalogService eventCatalogService, ILogger<GetEventAvailabilityAgentTool> logger)
    {
        _eventCatalogService = eventCatalogService;
        _logger = logger;
    }

    [Description("Checks the current reservation availability for a specific event: capacity, how many " +
                 "reservations exist, how many places remain, and whether it can still accept reservations.")]
    public async Task<EventAvailabilityDto?> GetEventAvailability(
        [Description("The id of the event to check.")] int eventId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _eventCatalogService.GetEventAvailabilityAsync(eventId, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetEventAvailability agent tool call failed for event {EventId}.", eventId);
            throw;
        }
    }
}
