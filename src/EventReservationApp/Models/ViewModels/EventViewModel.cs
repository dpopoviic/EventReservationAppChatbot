using System.ComponentModel.DataAnnotations;

namespace EventReservationApp.Models.ViewModels;

/// <summary>
/// ViewModel used for creating and editing events. Kept separate from the
/// Event entity so the presentation layer never depends directly on the
/// EF Core model (and so we can add UI-only validation/fields later).
/// </summary>
public class EventFormViewModel
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(2000)]
    [DataType(DataType.MultilineText)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Location { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Start Date")]
    [DataType(DataType.DateTime)]
    public DateTime StartDate { get; set; } = DateTime.Now.AddDays(7);

    [Required]
    [Display(Name = "End Date")]
    [DataType(DataType.DateTime)]
    public DateTime EndDate { get; set; } = DateTime.Now.AddDays(7).AddHours(2);

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Capacity must be at least 1.")]
    public int Capacity { get; set; } = 50;
}

/// <summary>
/// ViewModel used for the events list/details pages, including a bit of
/// derived, read-only information (spots remaining, etc).
/// </summary>
public class EventListItemViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int Capacity { get; set; }
    public int ReservedCount { get; set; }
    public int SpotsRemaining => Capacity - ReservedCount;
    public bool IsFull => SpotsRemaining <= 0;

    /// <summary>True when the currently logged-in user already has a reservation for this event.</summary>
    public bool CurrentUserHasReservation { get; set; }
}
