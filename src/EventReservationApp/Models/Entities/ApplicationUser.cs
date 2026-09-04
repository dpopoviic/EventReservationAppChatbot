using Microsoft.AspNetCore.Identity;

namespace EventReservationApp.Models.Entities;

/// <summary>
/// Application user. Extends the built-in Identity user with a couple of
/// extra profile fields. Username/Email/PasswordHash/etc. are all handled
/// by ASP.NET Core Identity - do not manage those manually.
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string FullName => $"{FirstName} {LastName}".Trim();

    /// <summary>
    /// Navigation property: all reservations made by this user.
    /// </summary>
    public ICollection<EventReservation> EventReservations { get; set; } = new List<EventReservation>();
}
