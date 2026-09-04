using EventReservationApp.Models.Entities;
using EventReservationApp.Models.ViewModels;

namespace EventReservationApp.Services.Interfaces;

/// <summary>
/// Business logic for managing events. Kept out of controllers so that
/// controllers stay thin and the logic is independently testable/reusable.
/// </summary>
public interface IEventService
{
    Task<List<EventListItemViewModel>> GetAllAsync(string? currentUserId = null);

    Task<Event?> GetByIdAsync(int id);

    Task<EventListItemViewModel?> GetListItemByIdAsync(int id, string? currentUserId = null);

    Task<Event> CreateAsync(EventFormViewModel model);

    Task<bool> UpdateAsync(EventFormViewModel model);

    Task<bool> DeleteAsync(int id);

    Task<bool> ExistsAsync(int id);
}
