using EventReservationApp.Services.AgentTools;
using EventReservationApp.Services.Implementations;
using EventReservationApp.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EventReservationApp.Tests.AgentTools;

public class GetEventAvailabilityAgentToolTests
{
    private static GetEventAvailabilityAgentTool CreateTool(Data.ApplicationDbContext db, EventReservationApp.Services.Interfaces.ICurrentUser currentUser)
    {
        var catalogService = new EventCatalogService(db, currentUser);
        return new GetEventAvailabilityAgentTool(catalogService, NullLogger<GetEventAvailabilityAgentTool>.Instance);
    }

    [Fact]
    public async Task GetEventAvailability_ExistingEvent_ReturnsAvailabilityDetails()
    {
        await using var db = InMemoryDbContextFactory.Create();
        db.AddUser("user-1");
        db.AddEvent(1, name: "Event A", capacity: 10);
        db.AddReservation(1, "user-1", 1);
        await db.SaveChangesAsync();

        var tool = CreateTool(db, FakeCurrentUser.Anonymous());

        var result = await tool.GetEventAvailability(1);

        Assert.NotNull(result);
        Assert.Equal("Event A", result!.EventName);
        Assert.Equal(10, result.Capacity);
        Assert.Equal(1, result.ReservedCount);
        Assert.Equal(9, result.AvailablePlaces);
        Assert.True(result.IsAvailableForReservation);
    }

    [Fact]
    public async Task GetEventAvailability_NonexistentEvent_ReturnsNull()
    {
        await using var db = InMemoryDbContextFactory.Create();

        var tool = CreateTool(db, FakeCurrentUser.Anonymous());

        var result = await tool.GetEventAvailability(999);

        Assert.Null(result);
    }

    [Fact]
    public async Task GetEventAvailability_FullEvent_IsNotAvailableForReservation()
    {
        await using var db = InMemoryDbContextFactory.Create();
        db.AddUser("user-1");
        db.AddEvent(1, name: "Tiny Event", capacity: 1);
        db.AddReservation(1, "user-1", 1);
        await db.SaveChangesAsync();

        var tool = CreateTool(db, FakeCurrentUser.Anonymous());

        var result = await tool.GetEventAvailability(1);

        Assert.NotNull(result);
        Assert.False(result!.IsAvailableForReservation);
    }

    [Fact]
    public void GetEventAvailabilityMethod_HasNoIdentityShapedParameter()
    {
        var method = typeof(GetEventAvailabilityAgentTool).GetMethod(nameof(GetEventAvailabilityAgentTool.GetEventAvailability));
        Assert.NotNull(method);

        var forbiddenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "userid", "user_id", "currentuserid", "callerid", "onbehalfof", "username", "role", "isadministrator"
        };

        foreach (var parameter in method!.GetParameters())
        {
            Assert.False(
                forbiddenNames.Contains(parameter.Name ?? string.Empty),
                $"GetEventAvailability must never accept a '{parameter.Name}' parameter - identity has no bearing on availability checks.");
        }
    }
}
