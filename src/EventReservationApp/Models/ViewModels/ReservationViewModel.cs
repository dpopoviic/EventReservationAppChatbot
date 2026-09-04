using System.ComponentModel.DataAnnotations;

namespace EventReservationApp.Models.ViewModels;

/// <summary>
/// ViewModel used when a customer creates a reservation for an event.
/// </summary>
public class ReservationCreateViewModel
{
    public int EventId { get; set; }

    public string EventName { get; set; } = string.Empty;

    [StringLength(500)]
    [DataType(DataType.MultilineText)]
    public string? Notes { get; set; }
}

/// <summary>
/// ViewModel used to display a reservation (list, details) with the
/// related user/event information flattened for the view.
/// </summary>
public class ReservationListItemViewModel
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateTime EventStartDate { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserFullName { get; set; } = string.Empty;
    public string UserEmail { get; set; } = string.Empty;
    public DateTime ReservationDate { get; set; }
    public string? Notes { get; set; }
}
