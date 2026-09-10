using EventReservationApp.Mcp.Tools;
using EventReservationApp.Services.Implementations;
using EventReservationApp.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol;
using Xunit;

namespace EventReservationApp.Tests.Mcp;

public class GetEventAvailabilityToolTests
{
    private static GetEventAvailabilityTool CreateTool(Data.ApplicationDbContext db)
    {
        var catalogService = new EventCatalogService(db, FakeCurrentUser.Anonymous());
        return new GetEventAvailabilityTool(catalogService, NullLogger<GetEventAvailabilityTool>.Instance);
    }

    [Fact]
    public async Task ExistingEvent_ReturnsAvailability()
    {
        await using var db = InMemoryDbContextFactory.Create();
        db.AddUser("u1");
        db.AddUser("u2");
        db.AddEvent(1, name: "Conference", capacity: 10);
        db.AddReservation(1, "u1", 1);
        db.AddReservation(2, "u2", 1);
        await db.SaveChangesAsync();

        var tool = CreateTool(db);

        var availability = await tool.GetEventAvailability(1);

        Assert.Equal(1, availability.EventId);
        Assert.Equal("Conference", availability.EventName);
        Assert.Equal(10, availability.Capacity);
        Assert.Equal(2, availability.ReservedCount);
        Assert.Equal(8, availability.AvailablePlaces);
        Assert.True(availability.IsAvailableForReservation);
    }

    [Fact]
    public async Task FullEvent_ReportsUnavailable()
    {
        await using var db = InMemoryDbContextFactory.Create();
        db.AddUser("u1");
        db.AddUser("u2");
        db.AddEvent(1, name: "Small Workshop", capacity: 2);
        db.AddReservation(1, "u1", 1);
        db.AddReservation(2, "u2", 1);
        await db.SaveChangesAsync();

        var tool = CreateTool(db);

        var availability = await tool.GetEventAvailability(1);

        Assert.Equal(0, availability.AvailablePlaces);
        Assert.False(availability.IsAvailableForReservation);
    }

    [Fact]
    public async Task NonexistentEvent_ThrowsControlledMcpException()
    {
        await using var db = InMemoryDbContextFactory.Create();
        var tool = CreateTool(db);

        var ex = await Assert.ThrowsAsync<McpException>(() => tool.GetEventAvailability(999));

        Assert.DoesNotContain("Exception", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SqlException", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
