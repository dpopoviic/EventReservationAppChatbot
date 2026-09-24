using EventReservationApp.Models.Dtos;

namespace EventReservationApp.Services.Interfaces
{
    public interface IEventCatalogService
    {
        Task<List<EventSearchResultDto>> SearchEventsAsync(
      EventSearchFilter filter,
      CancellationToken cancellationToken = default);
        Task<EventAvailabilityDto?> GetEventAvailabilityAsync(
       int eventId,
       CancellationToken cancellationToken = default);
        Task<IReadOnlyList<EventCandidateDto>> ResolveByNameAsync(
       string name,
       CancellationToken cancellationToken = default);
    }
}
