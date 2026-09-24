using EventReservationApp.Models.Dtos;
using EventReservationApp.Services.Auth;
using EventReservationApp.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventReservationApp.Controllers.Api;

[ApiController]
[Route("api/internal")]
[Authorize(AuthenticationSchemes = InternalApiKeyHandler.SchemeName)]
public class InternalAgentController : ControllerBase
{
    private readonly IEventCatalogService _eventCatalogService;
    private readonly IMyReservationsService _myReservationsService;

    public InternalAgentController(
        IEventCatalogService eventCatalogService,
        IMyReservationsService myReservationsService)
    {
        _eventCatalogService = eventCatalogService;
        _myReservationsService = myReservationsService;
    }

    // GET /api/internal/events/search?searchTerm=&startDate=&endDate=&location=&availability=
    [HttpGet("events/search")]
    public async Task<ActionResult<List<EventSearchResultDto>>> SearchEvents(
        [FromQuery] string? searchTerm,
        [FromQuery] DateTime? startDate,
        [FromQuery] DateTime? endDate,
        [FromQuery] string? location,
        [FromQuery] bool? availability,
        CancellationToken cancellationToken)
    {
        var filter = new EventSearchFilter
        {
            SearchTerm = searchTerm,
            StartDateFrom = startDate,
            StartDateTo = endDate,
            Location = location,
            OnlyAvailable = availability
        };

        var results = await _eventCatalogService.SearchEventsAsync(filter, cancellationToken);
        return Ok(results);
    }

    // GET /api/internal/events/resolve?name=
    [HttpGet("events/resolve")]
    public async Task<ActionResult<IReadOnlyList<EventCandidateDto>>> ResolveEvent(
        [FromQuery] string name,
        CancellationToken cancellationToken)
    {
        var candidates = await _eventCatalogService.ResolveByNameAsync(name, cancellationToken);
        return Ok(candidates);
    }

    // GET /api/internal/events/{eventId}/availability
    [HttpGet("events/{eventId:int}/availability")]
    public async Task<ActionResult<EventAvailabilityDto>> GetEventAvailability(
        int eventId,
        CancellationToken cancellationToken)
    {
        var availability = await _eventCatalogService.GetEventAvailabilityAsync(eventId, cancellationToken);
        return availability is null ? NotFound() : Ok(availability);
    }

    // GET /api/internal/reservations
    [HttpGet("reservations")]
    public async Task<ActionResult<List<MyReservationDto>>> GetMyReservations(
        CancellationToken cancellationToken)
    {
        var reservations = await _myReservationsService.GetMyReservationsAsync(cancellationToken);
        return Ok(reservations);
    }

    // POST /api/internal/reservations  { eventId, notes }
    [HttpPost("reservations")]
    public async Task<ActionResult<ReservationOperationResult>> Reserve(
        [FromBody] ReserveRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _myReservationsService.ReserveForCurrentUserAsync(
            request.EventId, request.Notes, cancellationToken);
        return Ok(result);
    }

    // DELETE /api/internal/reservations/{reservationId}
    [HttpDelete("reservations/{reservationId:int}")]
    public async Task<ActionResult<ReservationOperationResult>> Cancel(
        int reservationId,
        CancellationToken cancellationToken)
    {
        var result = await _myReservationsService.CancelForCurrentUserAsync(reservationId, cancellationToken);
        return Ok(result);
    }

    public sealed class ReserveRequest
    {
        public int EventId { get; set; }
        public string? Notes { get; set; }
    }
}
