using EventReservationApp.Models.ViewModels;

namespace EventReservationApp.Services.Interfaces;

/// <summary>
/// Business logic for creating, listing and cancelling reservations,
/// including capacity enforcement.
/// </summary>
public interface IReservationService
{
    /// <summary>All reservations in the system (administrator view).</summary>
    Task<List<ReservationListItemViewModel>> GetAllAsync();

    /// <summary>Reservations belonging to a single user (customer's "My Reservations" view).</summary>
    Task<List<ReservationListItemViewModel>> GetForUserAsync(string userId);

    Task<ReservationListItemViewModel?> GetByIdAsync(int id);

    /// <summary>
    /// Attempts to create a reservation. Fails if the event is already at
    /// capacity or the user already has a reservation for this event.
    /// </summary>
    Task<(bool Success, string? ErrorMessage)> CreateAsync(string userId, int eventId, string? notes);

    /// <summary>
    /// Deletes a reservation. The caller is responsible for verifying the
    /// current user is allowed to delete it (owner or administrator).
    /// </summary>
    Task<bool> DeleteAsync(int id);

    Task<bool> IsOwnedByUserAsync(int reservationId, string userId);
}
