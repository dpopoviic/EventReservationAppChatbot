using EventReservationApp.Data;
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

            if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
            {
                var term = filter.SearchTerm.Trim().ToLower();
                query = query.Where(e =>
                    e.Name.ToLower().Contains(term) ||
                    e.Description.ToLower().Contains(term));
            }

            if (!string.IsNullOrWhiteSpace(filter.Location))
            {
                var location = filter.Location.Trim().ToLower();
                query = query.Where(e => e.Location.ToLower().Contains(location));
            }

            if (filter.StartDateFrom.HasValue)
            {
                query = query.Where(e => e.StartDate >= filter.StartDateFrom.Value);
            }

            if (filter.StartDateTo.HasValue)
            {
                query = query.Where(e => e.StartDate <= filter.StartDateTo.Value);
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
    }

}
