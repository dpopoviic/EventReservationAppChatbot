using System.ComponentModel;
using EventReservationApp.Services.Interfaces;

namespace EventReservationApp.Services.AgentTools;

/// <summary>
/// Agent tool surface for the current user's own reservations. Thin wrapper
/// over <see cref="IMyReservationsService"/>, which resolves identity solely
/// from <see cref="ICurrentUser"/> (populated by the authenticated request) -
/// there is deliberately no parameter anywhere on this tool through which a
/// caller (or the model) could supply a user id, override identity, or act on
/// another user's behalf. Ownership, capacity and duplicate-reservation rules
/// are all enforced by the application service, never here. Plain class, no
/// MCP attributes; [Description] attributes are read by AIFunctionFactory.Create
/// to build the tool's schema.
/// </summary>
public sealed class ManageMyReservationsAgentTool
{
    private readonly IMyReservationsService _myReservationsService;
    private readonly ILogger<ManageMyReservationsAgentTool> _logger;

    public ManageMyReservationsAgentTool(IMyReservationsService myReservationsService, ILogger<ManageMyReservationsAgentTool> logger)
    {
        _myReservationsService = myReservationsService;
        _logger = logger;
    }

    [Description(
        "Manages reservations for the currently authenticated user. Use operation=\"list\" to retrieve " +
        "the user's own reservations, operation=\"reserve\" to create a reservation for the current user, " +
        "and operation=\"cancel\" to cancel one of the current user's own reservations. User identity is " +
        "determined by the application from the authenticated request and can never be supplied as a " +
        "parameter - this tool cannot list, create, or cancel reservations for anyone other than the " +
        "signed-in caller.")]
    public async Task<ManageMyReservationsAgentToolResult> ManageMyReservations(
        [Description("The operation to perform. One of: \"list\", \"reserve\", \"cancel\".")]
        string operation,
        [Description("The id of the event to reserve. Required when operation=\"reserve\"; ignored otherwise.")]
        int? eventId = null,
        [Description("Optional free-text note to attach to the new reservation. Only used when operation=\"reserve\".")]
        string? notes = null,
        [Description("The id of the reservation to cancel. Required when operation=\"cancel\"; ignored otherwise. " +
                     "Must already belong to the current user - reservations belonging to other users can never " +
                     "be looked up or cancelled through this tool.")]
        int? reservationId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedOperation = operation?.Trim().ToLowerInvariant();

        try
        {
            return normalizedOperation switch
            {
                "list" => await ListAsync(cancellationToken),
                "reserve" => await ReserveAsync(eventId, notes, cancellationToken),
                "cancel" => await CancelAsync(reservationId, cancellationToken),
                null or "" => throw new ArgumentException("operation is required. Valid values are: list, reserve, cancel."),
                _ => throw new ArgumentException($"Unknown operation '{operation}'. Valid values are: list, reserve, cancel.")
            };
        }
        catch (UnauthorizedAccessException)
        {
            // Thrown by ICurrentUser.RequireUserId() when there is no authenticated
            // user on the request. There is no MCP exception-translation layer here,
            // so this is turned into a controlled failure result instead of being
            // allowed to throw out of an agent tool invocation.
            return ManageMyReservationsAgentToolResult.ForNotSignedIn(normalizedOperation ?? operation ?? string.Empty);
        }
        catch (Exception ex) when (ex is not ArgumentException)
        {
            _logger.LogError(ex, "ManageMyReservations agent tool call failed for operation '{Operation}'.", operation);
            throw;
        }
    }

    private async Task<ManageMyReservationsAgentToolResult> ListAsync(CancellationToken cancellationToken)
    {
        var reservations = await _myReservationsService.GetMyReservationsAsync(cancellationToken);
        return ManageMyReservationsAgentToolResult.ForList(reservations);
    }

    private async Task<ManageMyReservationsAgentToolResult> ReserveAsync(int? eventId, string? notes, CancellationToken cancellationToken)
    {
        if (eventId is null)
        {
            throw new ArgumentException("eventId is required when operation=\"reserve\".");
        }

        var result = await _myReservationsService.ReserveForCurrentUserAsync(eventId.Value, notes, cancellationToken);
        return ManageMyReservationsAgentToolResult.ForReserve(result);
    }

    private async Task<ManageMyReservationsAgentToolResult> CancelAsync(int? reservationId, CancellationToken cancellationToken)
    {
        if (reservationId is null)
        {
            throw new ArgumentException("reservationId is required when operation=\"cancel\".");
        }

        var result = await _myReservationsService.CancelForCurrentUserAsync(reservationId.Value, cancellationToken);
        return ManageMyReservationsAgentToolResult.ForCancel(result);
    }
}
