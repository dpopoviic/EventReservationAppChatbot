using EventReservationApp.Models.Dtos;
using EventReservationApp.Services.Interfaces;

namespace EventReservationApp.Services.Implementations
{
    public class MyReservationsService : IMyReservationsService
    {
        private readonly IReservationService _reservationService;
        private readonly ICurrentUser _currentUser;

        public MyReservationsService(IReservationService reservationService, ICurrentUser currentUser)
        {
            _reservationService = reservationService;
            _currentUser = currentUser;
        }

        public async Task<List<MyReservationDto>> GetMyReservationsAsync(CancellationToken cancellationToken = default)
        {
            var userId = _currentUser.RequireUserId();

            var reservations = await _reservationService.GetForUserAsync(userId);

            return reservations
                .Select(r => new MyReservationDto
                {
                    ReservationId = r.Id,
                    EventId = r.EventId,
                    EventName = r.EventName,
                    EventStartDate = r.EventStartDate,
                    ReservationDate = r.ReservationDate,
                    Notes = r.Notes
                })
                .ToList();
        }

        public async Task<ReservationOperationResult> ReserveForCurrentUserAsync(
            int eventId,
            string? notes,
            CancellationToken cancellationToken = default)
        {
            var userId = _currentUser.RequireUserId();

            var (success, errorMessage) = await _reservationService.CreateAsync(userId, eventId, notes);

            return success
                ? ReservationOperationResult.Ok()
                : ReservationOperationResult.Fail(errorMessage ?? "Unable to create the reservation.");
        }

        public async Task<ReservationOperationResult> CancelForCurrentUserAsync(
            int reservationId,
            CancellationToken cancellationToken = default)
        {
            var userId = _currentUser.RequireUserId();

            // Ownership check happens BEFORE any delete - and is based solely on
            // the trusted current user id, never on anything the caller passes in.
            var isOwnedByCurrentUser = await _reservationService.IsOwnedByUserAsync(reservationId, userId);
            if (!isOwnedByCurrentUser)
            {
                return ReservationOperationResult.Fail("Reservation not found.");
            }

            var deleted = await _reservationService.DeleteAsync(reservationId);
            return deleted
                ? ReservationOperationResult.Ok(reservationId)
                : ReservationOperationResult.Fail("Reservation not found.");
        }
    }

}
