using EventReservationApp.Data;
using EventReservationApp.Helpers;
using EventReservationApp.Models.Dtos;
using EventReservationApp.Services.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace EventReservationApp.Services.Implementations
{
    public class EventCatalogService : IEventCatalogService
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrentUser _currentUser;

        public EventCatalogService(ApplicationDbContext context, ICurrentUser currentUser)
        {
            _context = context;
            _currentUser = currentUser;
        }

        public async Task<List<EventSearchResultDto>> SearchEventsAsync(
            EventSearchFilter filter,
            CancellationToken cancellationToken = default)
        {
            var currentUserId = _currentUser.UserId;

            var query = _context.Events.AsQueryable();

            if (filter.StartDateFrom.HasValue)
            {
                query = query.Where(e => e.StartDate >= filter.StartDateFrom.Value);
            }

            if (filter.StartDateTo.HasValue)
            {
                // Ukljucuje ceo poslednji dan, i dogadjaje koji pocinju u toku tog dana.
                var endExclusive = filter.StartDateTo.Value.Date.AddDays(1);
                query = query.Where(e => e.StartDate < endExclusive);
            }

            var results = await query
                .OrderBy(e => e.StartDate)
                .Select(e => new EventSearchResultDto
                {
                    EventId = e.Id,
                    Name = e.Name,
                    Description = e.Description,
                    Location = e.Location,
                    StartDate = e.StartDate,
                    EndDate = e.EndDate,
                    Capacity = e.Capacity,
                    ReservedCount = e.EventReservations.Count,
                    CurrentUserHasReservation = currentUserId != null &&
                        e.EventReservations.Any(r => r.UserId == currentUserId)
                })
                .ToListAsync(cancellationToken);

            // Transliteracija ne moze da se prevede u SQL, pa se tekst poredi u memoriji.
            var term = TextNormalizer.Normalize(filter.SearchTerm);
            if (term.Length > 0)
            {
                results = results
                    .Where(r =>
                        TextNormalizer.Normalize(r.Name).Contains(term) ||
                        TextNormalizer.Normalize(r.Description).Contains(term))
                    .ToList();
            }

            var location = TextNormalizer.Normalize(filter.Location);
            if (location.Length > 0)
            {
                results = results
                    .Where(r => TextNormalizer.Normalize(r.Location).Contains(location))
                    .ToList();
            }

            foreach (var result in results)
            {
                result.AvailablePlaces = Math.Max(0, result.Capacity - result.ReservedCount);
                result.IsFull = result.ReservedCount >= result.Capacity;
            }

            if (filter.OnlyAvailable == true)
            {
                results = results.Where(r => !r.IsFull).ToList();
            }

            return results;
        }

        public async Task<EventAvailabilityDto?> GetEventAvailabilityAsync(
            int eventId,
            CancellationToken cancellationToken = default)
        {
            var @event = await _context.Events
                .Where(e => e.Id == eventId)
                .Select(e => new
                {
                    e.Id,
                    e.Name,
                    e.Capacity,
                    ReservedCount = e.EventReservations.Count
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (@event is null)
            {
                return null;
            }

            var availablePlaces = Math.Max(0, @event.Capacity - @event.ReservedCount);

            return new EventAvailabilityDto
            {
                EventId = @event.Id,
                EventName = @event.Name,
                Capacity = @event.Capacity,
                ReservedCount = @event.ReservedCount,
                AvailablePlaces = availablePlaces,
                IsAvailableForReservation = availablePlaces > 0
            };
        }

        public async Task<IReadOnlyList<EventCandidateDto>> ResolveByNameAsync(
            string name,
            CancellationToken cancellationToken = default)
        {
            var term = TextNormalizer.Normalize(name);
            if (term.Length == 0)
            {
                return Array.Empty<EventCandidateDto>();
            }

            // Samo buduci dogadjaji se nude za rezervaciju.
            // Transliteracija ne moze da se prevede u SQL, pa se poredi u memoriji.
            var today = DateTime.Today;
            var events = await _context.Events
                .Where(e => e.StartDate >= today)
                .Select(e => new { e.Id, e.Name, e.StartDate })
                .ToListAsync(cancellationToken);

            var exact = events.Where(e => TextNormalizer.Normalize(e.Name) == term).ToList();
            var matches = exact.Count > 0
                ? exact
                : events.Where(e => TextNormalizer.Normalize(e.Name).Contains(term)).ToList();
            var matchType = exact.Count > 0 ? "exact" : "contains";

            return matches
                .OrderBy(e => e.StartDate)
                .Select(e => new EventCandidateDto(e.Id, e.Name, e.StartDate, matchType))
                .ToList();
        }
    }

}
