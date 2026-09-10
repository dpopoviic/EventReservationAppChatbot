using EventReservationApp.Services.Implementations;
using EventReservationApp.Tests.TestHelpers;
using Xunit;

namespace EventReservationApp.Tests;

public class MyReservationsServiceTests
{
    private static (MyReservationsService Service, EventReservationApp.Data.ApplicationDbContext Db) CreateService(
        EventReservationApp.Data.ApplicationDbContext db,
        EventReservationApp.Services.Interfaces.ICurrentUser currentUser)
    {
        var reservationService = new ReservationService(db);
        var service = new MyReservationsService(reservationService, currentUser);
        return (service, db);
    }

    // -----------------------------------------------------------------
    // GetMyReservationsAsync
    // -----------------------------------------------------------------

    [Fact]
    public async Task GetMyReservationsAsync_ReturnsOnlyCurrentUsersReservations()
    {
        await using var db = InMemoryDbContextFactory.Create();
        db.AddUser("user-1");
        db.AddUser("user-2");
        db.AddEvent(1, name: "Event A");
        db.AddEvent(2, name: "Event B");
        db.AddReservation(1, "user-1", 1);
        db.AddReservation(2, "user-2", 2); // belongs to a different user
        await db.SaveChangesAsync();

        var (service, _) = CreateService(db, FakeCurrentUser.For("user-1"));

        var reservations = await service.GetMyReservationsAsync();

        var reservation = Assert.Single(reservations);
        Assert.Equal("Event A", reservation.EventName);
    }

    [Fact]
    public async Task GetMyReservationsAsync_Unauthenticated_ThrowsUnauthorizedAccessException()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var (service, _) = CreateService(db, FakeCurrentUser.Anonymous());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.GetMyReservationsAsync());
    }

    // -----------------------------------------------------------------
    // ReserveForCurrentUserAsync
    // -----------------------------------------------------------------

    [Fact]
    public async Task ReserveForCurrentUserAsync_ValidRequest_CreatesReservationForCurrentUser()
    {
        await using var db = InMemoryDbContextFactory.Create();
        db.AddUser("user-1");
        db.AddEvent(1, name: "Event A", capacity: 5);
        await db.SaveChangesAsync();

        var (service, _) = CreateService(db, FakeCurrentUser.For("user-1"));

        var result = await service.ReserveForCurrentUserAsync(1, "Looking forward to it");

        Assert.True(result.Success);
        Assert.Null(result.ErrorMessage);

        var reservations = await service.GetMyReservationsAsync();
        var reservation = Assert.Single(reservations);
        Assert.Equal(1, reservation.EventId);
        Assert.Equal("Looking forward to it", reservation.Notes);
    }

    [Fact]
    public async Task ReserveForCurrentUserAsync_DuplicateReservation_IsRejected()
    {
        await using var db = InMemoryDbContextFactory.Create();
        db.AddUser("user-1");
        db.AddEvent(1, name: "Event A", capacity: 5);
        db.AddReservation(1, "user-1", 1);
        await db.SaveChangesAsync();

        var (service, _) = CreateService(db, FakeCurrentUser.For("user-1"));

        var result = await service.ReserveForCurrentUserAsync(1, null);

        Assert.False(result.Success);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task ReserveForCurrentUserAsync_EventAtCapacity_IsRejected()
    {
        await using var db = InMemoryDbContextFactory.Create();
        db.AddUser("user-1");
        db.AddUser("user-2");
        db.AddEvent(1, name: "Full Event", capacity: 1);
        db.AddReservation(1, "user-1", 1);
        await db.SaveChangesAsync();

        var (service, _) = CreateService(db, FakeCurrentUser.For("user-2"));

        var result = await service.ReserveForCurrentUserAsync(1, null);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task ReserveForCurrentUserAsync_Unauthenticated_ThrowsUnauthorizedAccessException()
    {
        await using var db = InMemoryDbContextFactory.Create();
        db.AddEvent(1, name: "Event A");
        await db.SaveChangesAsync();

        var (service, _) = CreateService(db, FakeCurrentUser.Anonymous());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.ReserveForCurrentUserAsync(1, null));
    }

    // -----------------------------------------------------------------
    // CancelForCurrentUserAsync
    // -----------------------------------------------------------------

    [Fact]
    public async Task CancelForCurrentUserAsync_OwnReservation_Cancels()
    {
        await using var db = InMemoryDbContextFactory.Create();
        db.AddUser("user-1");
        db.AddEvent(1, name: "Event A");
        db.AddReservation(42, "user-1", 1);
        await db.SaveChangesAsync();

        var (service, _) = CreateService(db, FakeCurrentUser.For("user-1"));

        var result = await service.CancelForCurrentUserAsync(42);

        Assert.True(result.Success);

        var remaining = await service.GetMyReservationsAsync();
        Assert.Empty(remaining);
    }

    [Fact]
    public async Task CancelForCurrentUserAsync_AnotherUsersReservation_IsRejectedAndNotDeleted()
    {
        await using var db = InMemoryDbContextFactory.Create();
        db.AddUser("owner");
        db.AddUser("attacker");
        db.AddEvent(1, name: "Event A");
        db.AddReservation(42, "owner", 1);
        await db.SaveChangesAsync();

        var (service, _) = CreateService(db, FakeCurrentUser.For("attacker"));

        var result = await service.CancelForCurrentUserAsync(42);

        Assert.False(result.Success);

        // The reservation must still exist, untouched, for its real owner.
        var ownerService = CreateService(db, FakeCurrentUser.For("owner")).Service;
        var ownerReservations = await ownerService.GetMyReservationsAsync();
        Assert.Single(ownerReservations);
    }

    [Fact]
    public async Task CancelForCurrentUserAsync_NonexistentReservation_IsRejected()
    {
        await using var db = InMemoryDbContextFactory.Create();
        db.AddUser("user-1");
        await db.SaveChangesAsync();

        var (service, _) = CreateService(db, FakeCurrentUser.For("user-1"));

        var result = await service.CancelForCurrentUserAsync(999);

        Assert.False(result.Success);
    }

    [Fact]
    public async Task CancelForCurrentUserAsync_Unauthenticated_ThrowsUnauthorizedAccessException()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var (service, _) = CreateService(db, FakeCurrentUser.Anonymous());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.CancelForCurrentUserAsync(1));
    }
}
