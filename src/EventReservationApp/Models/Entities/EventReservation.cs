using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EventReservationApp.Models.Entities;

/// <summary>
/// Represents a single user's reservation for a single event.
/// One User -&gt; many EventReservations, one Event -&gt; many EventReservations.
/// </summary>
public class EventReservation
{
    public int Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }

    [Required]
    public int EventId { get; set; }

    [ForeignKey(nameof(EventId))]
    public Event? Event { get; set; }

    [Required]
    [Display(Name = "Reservation Date")]
    public DateTime ReservationDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Optional free-text note the user can leave with their reservation.
    /// </summary>
    [StringLength(500)]
    public string? Notes { get; set; }
}
