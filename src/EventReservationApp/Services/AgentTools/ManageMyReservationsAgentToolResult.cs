using EventReservationApp.Models.Dtos;

namespace EventReservationApp.Services.AgentTools;

/// <summary>
/// Uniform, structured result shape returned by the ManageMyReservations agent
/// tool for all three operations, plus the "not signed in" case. Keeping one
/// shape (rather than a different .NET type per operation) gives the calling
/// model a predictable, documented output schema regardless of which
/// operation was requested.
/// </summary>
public sealed class ManageMyReservationsAgentToolResult
{
    public required string Operation { get; init; }
    public required bool Success { get; init; }
    public string? Message { get; init; }
    public IReadOnlyList<MyReservationDto>? Reservations { get; init; }
    public int? ReservationId { get; init; }

    public static ManageMyReservationsAgentToolResult ForList(IReadOnlyList<MyReservationDto> reservations) => new()
    {
        Operation = "list",
        Success = true,
        Reservations = reservations
    };

    public static ManageMyReservationsAgentToolResult ForReserve(ReservationOperationResult result) => new()
    {
        Operation = "reserve",
        Success = result.Success,
        Message = result.Success ? "Reservation created." : result.ErrorMessage,
        ReservationId = result.ReservationId
    };

    public static ManageMyReservationsAgentToolResult ForCancel(ReservationOperationResult result) => new()
    {
        Operation = "cancel",
        Success = result.Success,
        Message = result.Success ? "Reservation cancelled." : result.ErrorMessage,
        ReservationId = result.ReservationId
    };

    public static ManageMyReservationsAgentToolResult ForNotSignedIn(string operation) => new()
    {
        Operation = operation,
        Success = false,
        Message = "You must be signed in to manage reservations."
    };
}
