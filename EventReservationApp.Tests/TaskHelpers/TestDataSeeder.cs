using EventReservationApp.Data;
using EventReservationApp.Models.Entities;

namespace EventReservationApp.Tests.TestHelpers;

/// <summary>
/// Small extension methods for building up test data directly against an
/// in-memory <see cref="ApplicationDbContext"/>, mirroring the shape
/// <see cref="Data.DbInitializer"/> seeds in the real app.
/// </summary>
public static class TestDataSeeder
{
    public static ApplicationUser AddUser(
        this ApplicationDbContext db,
        string id,
        string firstName = "Test",
        string lastName = "User",
        string? email = null)
    {
        var resolvedEmail = email ?? $"{id}@example.com";

        var user = new ApplicationUser
        {
            Id = id,
            UserName = resolvedEmail,
            Email = resolvedEmail,
            FirstName = firstName,
            LastName = lastName
        };

        db.Users.Add(user);
        return user;
    }

    public static Event AddEvent(
        this ApplicationDbContext db,
        int id,
        string name = "Sample Event",
        string description = "Sample description",
        string location = "Sample Location",
        DateTime? startDate = null,
        DateTime? endDate = null,
        int capacity = 10)
    {
        var start = startDate ?? DateTime.UtcNow.AddDays(7);
        var end = endDate ?? start.AddHours(2);

        var @event = new Event
        {
            Id = id,
            Name = name,
            Description = description,
            Location = location,
            StartDate = start,
            EndDate = end,
            Capacity = capacity
        };

        db.Events.Add(@event);
        return @event;
    }

    public static EventReservation AddReservation(
        this ApplicationDbContext db,
        int id,
        string userId,
        int eventId,
        DateTime? reservationDate = null,
        string? notes = null)
    {
        var reservation = new EventReservation
        {
            Id = id,
            UserId = userId,
            EventId = eventId,
            ReservationDate = reservationDate ?? DateTime.UtcNow,
            Notes = notes
        };

        db.EventReservations.Add(reservation);
        return reservation;
    }
}
