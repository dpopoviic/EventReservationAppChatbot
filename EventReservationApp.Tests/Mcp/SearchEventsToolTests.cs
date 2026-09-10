using EventReservationApp.Mcp.Tools;
using EventReservationApp.Services.Implementations;
using EventReservationApp.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EventReservationApp.Tests.Mcp;

public class SearchEventsToolTests
{
    private static SearchEventsTool CreateTool(Data.ApplicationDbContext db, Services.Interfaces.ICurrentUser? currentUser = null)
    {
        var catalogService = new EventCatalogService(db, currentUser ?? FakeCurrentUser.Anonymous());
        return new SearchEventsTool(catalogService, NullLogger<SearchEventsTool>.Instance);
    }

    [Fact]
    public async Task ValidQuery_ReturnsMatchingEvents()
    {
        await using var db = InMemoryDbContextFactory.Create();
        db.AddEvent(1, name: "ASP.NET Core Conference", description: "All things .NET");
        db.AddEvent(2, name: "Local Meetup", description: "Casual gathering");
        await db.SaveChangesAsync();

        var tool = CreateTool(db);

        var results = await tool.SearchEvents(searchTerm: "conference");

        var result = Assert.Single(results);
        Assert.Equal(1, result.EventId);
        Assert.Equal("ASP.NET Core Conference", result.Name);
    }

    [Fact]
    public async Task Filtering_ByLocationAndAvailability_NarrowsResults()
    {
        await using var db = InMemoryDbContextFactory.Create();
        db.AddUser("u1");
        db.AddEvent(1, name: "Full Event", location: "Belgrade", capacity: 1);
        db.AddEvent(2, name: "Open Event", location: "Belgrade", capacity: 10);
        db.AddEvent(3, name: "Other City Event", location: "Novi Sad", capacity: 10);
        db.AddReservation(100, "u1", 1); // fills event 1 to capacity
        await db.SaveChangesAsync();

        var tool = CreateTool(db);

        var belgradeOnly = await tool.SearchEvents(location: "Belgrade");
        Assert.Equal(2, belgradeOnly.Count);

        var availableInBelgrade = await tool.SearchEvents(location: "Belgrade", availability: true);
        var onlyResult = Assert.Single(availableInBelgrade);
        Assert.Equal("Open Event", onlyResult.Name);
    }

    [Fact]
    public async Task NoMatches_ReturnsEmptyList()
    {
        await using var db = InMemoryDbContextFactory.Create();
        db.AddEvent(1, name: "Some Event");
        await db.SaveChangesAsync();

        var tool = CreateTool(db);

        var results = await tool.SearchEvents(searchTerm: "no-such-event-exists");

        Assert.Empty(results);
    }
}
