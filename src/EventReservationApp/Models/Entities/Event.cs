using System.ComponentModel.DataAnnotations;

namespace EventReservationApp.Models.Entities;

/// <summary>
/// Represents an event that users can reserve a spot for.
/// </summary>
public class Event
{
    public int Id { get; set; }

    [Required]
    [StringLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [StringLength(200)]
    public string Location { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Start Date")]
    [DataType(DataType.DateTime)]
    public DateTime StartDate { get; set; }

    [Required]
    [Display(Name = "End Date")]
    [DataType(DataType.DateTime)]
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Maximum number of reservations allowed for this event.
    /// Reservation creation is blocked once this capacity is reached.
    /// </summary>
    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Capacity must be at least 1.")]
    public int Capacity { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Navigation property: all reservations made for this event.
    /// </summary>
    public ICollection<EventReservation> EventReservations { get; set; } = new List<EventReservation>();
}
