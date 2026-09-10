using EventReservationApp.Models.Dtos;
using EventReservationApp.Services.Implementations;
using EventReservationApp.Tests.TestHelpers;
using Xunit;

namespace EventReservationApp.Tests;

public class EventCatalogServiceTests
{
    // -----------------------------------------------------------------
    // SearchEvents
    // -----------------------------------------------------------------

    [Fact]
    public async Task SearchEventsAsync_NoFilter_ReturnsAllEventsOrderedByStartDate()
    {
        await using var db = InMemoryDbContextFactory.Create();
        db.AddEvent(1, name: "Later Event", startDate: DateTime.UtcNow.AddDays(20));
        db.AddEvent(2, name: "Sooner Event", startDate: DateTime.UtcNow.AddDays(5));
        await db.SaveChangesAsync();

        var service = new EventCatalogService(db, FakeCurrentUser.Anonymous());

        var results = await service.SearchEventsAsync(new EventSearchFilter());

        Assert.Equal(2, results.Count);
        Assert.Equal("Sooner Event", results[0].Name);
        Assert.Equal("Later Event", results[1].Name);
    }

    [Fact]
    public async Task SearchEventsAsync_ValidSearchTerm_MatchesNameCaseInsensitively()
    {
        await using var db = InMemoryDbContextFactory.Create();
        db.AddEvent(1, name: "Community Jazz Night", description: "An evening of live music.");
        db.AddEvent(2, name: "Board Game Meetup", description: "Bring your favourite games.");
        await db.SaveChangesAsync();

        var service = new EventCatalogService(db, FakeCurrentUser.Anonymous());

        var results = await service.SearchEventsAsync(new EventSearchFilter { SearchTerm = "jazz" });

        var result = Assert.Single(results);
        Assert.Equal("Community Jazz Night", result.Name);
    }

    [Fact]
    public async Task SearchEventsAsync_SearchTerm_AlsoMatchesDescription()
    {
        await using var db = InMemoryDbContextFactory.Create();
        db.AddEvent(1, name: "Community Meetup", description: "Featuring a jazz trio.");
        db.AddEvent(2, name: "Board Game Night", description: "Bring your favourite games.");
        await db.SaveChangesAsync();

        var service = new EventCatalogService(db, FakeCurrentUser.Anonymous());

        var results = await service.SearchEventsAsync(new EventSearchFilter { SearchTerm = "jazz" });

        var result = Assert.Single(results);
        Assert.Equal("Community Meetup", result.Name);
    }

    [Fact]
    public async Task SearchEventsAsync_LocationFilter_ReturnsOnlyMatchingLocation()
    {
        await using var db = InMemoryDbContextFactory.Create();
        db.AddEvent(1, name: "Event A", location: "Downtown Hall");
        db.AddEvent(2, name: "Event B", location: "Riverside Park");
        await db.SaveChangesAsync();

        var service = new EventCatalogService(db, FakeCurrentUser.Anonymous());

        var results = await service.SearchEventsAsync(new EventSearchFilter { Location = "riverside" });

        var result = Assert.Single(results);
        Assert.Equal("Event B", result.Name);
    }

    [Fact]
    public async Task SearchEventsAsync_DateRangeFilter_ReturnsOnlyEventsInRange()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var now = DateTime.UtcNow;
        db.AddEvent(1, name: "Too Early", startDate: now.AddDays(1));
        db.AddEvent(2, name: "In Range", startDate: now.AddDays(10));
        db.AddEvent(3, name: "Too Late", startDate: now.AddDays(30));
        await db.SaveChangesAsync();

        var service = new EventCatalogService(db, FakeCurrentUser.Anonymous());

        var results = await service.SearchEventsAsync(new EventSearchFilter
        {
            StartDateFrom = now.AddDays(5),
            StartDateTo = now.AddDays(15)
        });

        var result = Assert.Single(results);
        Assert.Equal("In Range", result.Name);
    }

    [Fact]
    public async Task SearchEventsAsync_OnlyAvailable_ExcludesFullEvents()
    {
        await using var db = InMemoryDbContextFactory.Create();
        db.AddUser("user-1");
        db.AddEvent(1, name: "Full Event", capacity: 1);
        db.AddEvent(2, name: "Open Event", capacity: 5);
        db.AddReservation(1, "user-1", 1);
        await db.SaveChangesAsync();

        var service = new EventCatalogService(db, FakeCurrentUser.Anonymous());

        var results = await service.SearchEventsAsync(new EventSearchFilter { OnlyAvailable = true });

        var result = Assert.Single(results);
        Assert.Equal("Open Event", result.Name);
    }

    [Fact]
    public async Task SearchEventsAsync_PopulatesCurrentUserHasReservation_ForAuthenticatedUser()
    {
        await using var db = InMemoryDbContextFactory.Create();
        db.AddUser("user-1");
        db.AddUser("user-2");
        db.AddEvent(1, name: "Event A");
        db.AddReservation(1, "user-1", 1);
        await db.SaveChangesAsync();

        var serviceForOwner = new EventCatalogService(db, FakeCurrentUser.For("user-1"));
        var serviceForOther = new EventCatalogService(db, FakeCurrentUser.For("user-2"));

        var resultsForOwner = await serviceForOwner.SearchEventsAsync(new EventSearchFilter());
        var resultsForOther = await serviceForOther.SearchEventsAsync(new EventSearchFilter());

        Assert.True(resultsForOwner.Single().CurrentUserHasReservation);
        Assert.False(resultsForOther.Single().CurrentUserHasReservation);
    }

    [Fact]
    public async Task SearchEventsAsync_NoMatches_ReturnsEmptyList()
    {
        await using var db = InMemoryDbContextFactory.Create();
        db.AddEvent(1, name: "Community Jazz Night");
        await db.SaveChangesAsync();

        var service = new EventCatalogService(db, FakeCurrentUser.Anonymous());

        var results = await service.SearchEventsAsync(new EventSearchFilter { SearchTerm = "nonexistent-topic" });

        Assert.Empty(results);
    }

    // -----------------------------------------------------------------
    // GetEventAvailability
    // -----------------------------------------------------------------

    [Fact]
    public async Task GetEventAvailabilityAsync_ExistingAvailableEvent_ReturnsAvailability()
    {
        await using var db = InMemoryDbContextFactory.Create();
        db.AddUser("user-1");
        db.AddEvent(1, name: "Open Event", capacity: 5);
        db.AddReservation(1, "user-1", 1);
        await db.SaveChangesAsync();

        var service = new EventCatalogService(db, FakeCurrentUser.Anonymous());

        var availability = await service.GetEventAvailabilityAsync(1);

        Assert.NotNull(availability);
        Assert.Equal(1, availability!.EventId);
        Assert.Equal("Open Event", availability.EventName);
        Assert.Equal(5, availability.Capacity);
        Assert.Equal(1, availability.ReservedCount);
        Assert.Equal(4, availability.AvailablePlaces);
        Assert.True(availability.IsAvailableForReservation);
    }

    [Fact]
    public async Task GetEventAvailabilityAsync_NonexistentEvent_ReturnsNull()
    {
        await using var db = InMemoryDbContextFactory.Create();
        db.AddEvent(1, name: "Only Event");
        await db.SaveChangesAsync();

        var service = new EventCatalogService(db, FakeCurrentUser.Anonymous());

        var availability = await service.GetEventAvailabilityAsync(999);

        Assert.Null(availability);
    }

    [Fact]
    public async Task GetEventAvailabilityAsync_FullEvent_IsNotAvailableForReservation()
    {
        await using var db = InMemoryDbContextFactory.Create();
        db.AddUser("user-1");
        db.AddUser("user-2");
        db.AddEvent(1, name: "Full Event", capacity: 2);
        db.AddReservation(1, "user-1", 1);
        db.AddReservation(2, "user-2", 1);
        await db.SaveChangesAsync();

        var service = new EventCatalogService(db, FakeCurrentUser.Anonymous());

        var availability = await service.GetEventAvailabilityAsync(1);

        Assert.NotNull(availability);
        Assert.Equal(0, availability!.AvailablePlaces);
        Assert.False(availability.IsAvailableForReservation);
    }
}
