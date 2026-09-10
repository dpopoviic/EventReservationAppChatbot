using System.ComponentModel;
using EventReservationApp.Models.Dtos;
using EventReservationApp.Services.Interfaces;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace EventReservationApp.Mcp.Tools;

/// <summary>
/// MCP tool surface for checking a single event's reservation availability.
/// Thin wrapper over <see cref="IEventCatalogService"/> - capacity/availability
/// rules live entirely in the application service.
/// </summary>
[McpServerToolType]
public sealed class GetEventAvailabilityTool
{
    private readonly IEventCatalogService _eventCatalogService;
    private readonly ILogger<GetEventAvailabilityTool> _logger;

    public GetEventAvailabilityTool(IEventCatalogService eventCatalogService, ILogger<GetEventAvailabilityTool> logger)
    {
        _eventCatalogService = eventCatalogService;
        _logger = logger;
    }

    [McpServerTool(Name = "GetEventAvailability")]
    [Description("Checks the current reservation availability for a specific event: capacity, how many " +
                 "reservations exist, how many places remain, and whether it can still accept reservations.")]
    public async Task<EventAvailabilityDto> GetEventAvailability(
        [Description("The id of the event to check.")] int eventId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var availability = await _eventCatalogService.GetEventAvailabilityAsync(eventId, cancellationToken);
            if (availability is null)
            {
                throw new McpException($"No event was found with id {eventId}.");
            }

            return availability;
        }
        catch (Exception ex) when (ex is not McpException)
        {
            _logger.LogError(ex, "GetEventAvailability tool call failed for event {EventId}.", eventId);
            throw new McpException("Unable to check event availability right now. Please try again later.");
        }
    }
}
