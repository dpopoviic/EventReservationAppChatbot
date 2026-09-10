using EventReservationApp.Models.Dtos;

namespace EventReservationApp.Mcp.Tools;

/// <summary>
/// Uniform, structured result shape returned by the ManageMyReservations MCP
/// tool for all three operations. Keeping one shape (rather than a different
/// .NET type per operation) gives the calling model a predictable, documented
/// output schema regardless of which operation was requested.
/// </summary>
public sealed class ManageMyReservationsResult
{
    public required string Operation { get; init; }
    public required bool Success { get; init; }
    public string? Message { get; init; }
    public IReadOnlyList<MyReservationDto>? Reservations { get; init; }
    public int? ReservationId { get; init; }

    public static ManageMyReservationsResult ForList(IReadOnlyList<MyReservationDto> reservations) => new()
    {
        Operation = "list",
        Success = true,
        Reservations = reservations
    };

    public static ManageMyReservationsResult ForReserve(ReservationOperationResult result) => new()
    {
        Operation = "reserve",
        Success = result.Success,
        Message = result.Success ? "Reservation created." : result.ErrorMessage,
        ReservationId = result.ReservationId
    };

    public static ManageMyReservationsResult ForCancel(ReservationOperationResult result) => new()
    {
        Operation = "cancel",
        Success = result.Success,
        Message = result.Success ? "Reservation cancelled." : result.ErrorMessage,
        ReservationId = result.ReservationId
    };
}
