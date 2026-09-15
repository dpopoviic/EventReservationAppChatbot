using EventReservationApp.Services.AgentTools;
using EventReservationApp.Services.Implementations;
using EventReservationApp.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EventReservationApp.Tests.AgentTools;

public class SearchEventsAgentToolTests
{
    private static SearchEventsAgentTool CreateTool(Data.ApplicationDbContext db, EventReservationApp.Services.Interfaces.ICurrentUser currentUser)
    {
        var catalogService = new EventCatalogService(db, currentUser);
        return new SearchEventsAgentTool(catalogService, NullLogger<SearchEventsAgentTool>.Instance);
    }

    [Fact]
    public async Task SearchEvents_NoFilters_ReturnsAllEvents()
    {
        await using var db = InMemoryDbContextFactory.Create();
        db.AddEvent(1, name: "Event A");
        db.AddEvent(2, name: "Event B");
        await db.SaveChangesAsync();

        var tool = CreateTool(db, FakeCurrentUser.Anonymous());

        var results = await tool.SearchEvents();

        Assert.Equal(2, results.Count);
    }

    [Fact]
    public async Task SearchEvents_WithSearchTerm_FiltersByNameOrDescription()
    {
        await using var db = InMemoryDbContextFactory.Create();
        db.AddEvent(1, name: "Jazz Night", description: "Live jazz music");
        db.AddEvent(2, name: "Tech Conference", description: "Talks about software");
        await db.SaveChangesAsync();

        var tool = CreateTool(db, FakeCurrentUser.Anonymous());

        var results = await tool.SearchEvents(searchTerm: "jazz");

        var result = Assert.Single(results);
        Assert.Equal("Jazz Night", result.Name);
    }

    [Fact]
    public async Task SearchEvents_AvailabilityTrue_ExcludesFullEvents()
    {
        await using var db = InMemoryDbContextFactory.Create();
        db.AddUser("user-1");
        db.AddEvent(1, name: "Full Event", capacity: 1);
        db.AddEvent(2, name: "Open Event", capacity: 10);
        db.AddReservation(1, "user-1", 1);
        await db.SaveChangesAsync();

        var tool = CreateTool(db, FakeCurrentUser.Anonymous());

        var results = await tool.SearchEvents(availability: true);

        var result = Assert.Single(results);
        Assert.Equal("Open Event", result.Name);
    }

    [Fact]
    public void SearchEventsMethod_HasNoIdentityShapedParameter()
    {
        var method = typeof(SearchEventsAgentTool).GetMethod(nameof(SearchEventsAgentTool.SearchEvents));
        Assert.NotNull(method);

        var forbiddenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "userid", "user_id", "currentuserid", "callerid", "onbehalfof", "username", "role", "isadministrator"
        };

        foreach (var parameter in method!.GetParameters())
        {
            Assert.False(
                forbiddenNames.Contains(parameter.Name ?? string.Empty),
                $"SearchEvents must never accept a '{parameter.Name}' parameter - identity has no bearing on event search.");
        }
    }
}
