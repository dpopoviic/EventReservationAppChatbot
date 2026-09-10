using System.ComponentModel;
using EventReservationApp.Services.Interfaces;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace EventReservationApp.Mcp.Tools;

/// <summary>
/// MCP tool surface for the current user's own reservations. Thin wrapper
/// over <see cref="IMyReservationsService"/>, which resolves identity solely
/// from <see cref="ICurrentUser"/> (populated by the authenticated request) -
/// there is deliberately no parameter anywhere on this tool through which a
/// caller (or the model) could supply a user id, override identity, or act on
/// another user's behalf. Ownership, capacity and duplicate-reservation rules
/// are all enforced by the application service, never here.
/// </summary>
[McpServerToolType]
public sealed class ManageMyReservationsTool
{
    private readonly IMyReservationsService _myReservationsService;
    private readonly ILogger<ManageMyReservationsTool> _logger;

    public ManageMyReservationsTool(IMyReservationsService myReservationsService, ILogger<ManageMyReservationsTool> logger)
    {
        _myReservationsService = myReservationsService;
        _logger = logger;
    }

    [McpServerTool(Name = "ManageMyReservations")]
    [Description(
        "Manages reservations for the currently authenticated user. Use operation=\"list\" to retrieve " +
        "the user's own reservations, operation=\"reserve\" to create a reservation for the current user, " +
        "and operation=\"cancel\" to cancel one of the current user's own reservations. User identity is " +
        "determined by the application from the authenticated request and can never be supplied as a " +
        "parameter - this tool cannot list, create, or cancel reservations for anyone other than the " +
        "signed-in caller.")]
    public async Task<ManageMyReservationsResult> ManageMyReservations(
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
        try
        {
            return operation?.Trim().ToLowerInvariant() switch
            {
                "list" => await ListAsync(cancellationToken),
                "reserve" => await ReserveAsync(eventId, notes, cancellationToken),
                "cancel" => await CancelAsync(reservationId, cancellationToken),
                null or "" => throw new McpException("operation is required. Valid values are: list, reserve, cancel."),
                _ => throw new McpException($"Unknown operation '{operation}'. Valid values are: list, reserve, cancel.")
            };
        }
        catch (UnauthorizedAccessException)
        {
            // Thrown by ICurrentUser.RequireUserId() when there is no authenticated
            // user on the request - translated here into a controlled MCP error
            // instead of letting the raw exception (or its stack trace) escape.
            throw new McpException("You must be signed in to manage reservations.");
        }
        catch (Exception ex) when (ex is not McpException)
        {
            _logger.LogError(ex, "ManageMyReservations tool call failed for operation '{Operation}'.", operation);
            throw new McpException("Unable to complete the requested reservation operation right now. Please try again later.");
        }
    }

    private async Task<ManageMyReservationsResult> ListAsync(CancellationToken cancellationToken)
    {
        var reservations = await _myReservationsService.GetMyReservationsAsync(cancellationToken);
        return ManageMyReservationsResult.ForList(reservations);
    }

    private async Task<ManageMyReservationsResult> ReserveAsync(int? eventId, string? notes, CancellationToken cancellationToken)
    {
        if (eventId is null)
        {
            throw new McpException("eventId is required when operation=\"reserve\".");
        }

        var result = await _myReservationsService.ReserveForCurrentUserAsync(eventId.Value, notes, cancellationToken);
        return ManageMyReservationsResult.ForReserve(result);
    }

    private async Task<ManageMyReservationsResult> CancelAsync(int? reservationId, CancellationToken cancellationToken)
    {
        if (reservationId is null)
        {
            throw new McpException("reservationId is required when operation=\"cancel\".");
        }

        var result = await _myReservationsService.CancelForCurrentUserAsync(reservationId.Value, cancellationToken);
        return ManageMyReservationsResult.ForCancel(result);
    }
}
