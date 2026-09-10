using EventReservationApp.Models.Dtos;

namespace EventReservationApp.Services.Interfaces
{
    public interface IMyReservationsService
    {
        Task<List<MyReservationDto>> GetMyReservationsAsync(CancellationToken cancellationToken = default);
        Task<ReservationOperationResult> ReserveForCurrentUserAsync(int eventId, string? notes, CancellationToken cancellationToken = default);
        Task<ReservationOperationResult> CancelForCurrentUserAsync(int reservationId, CancellationToken cancellationToken = default);
    }
}
