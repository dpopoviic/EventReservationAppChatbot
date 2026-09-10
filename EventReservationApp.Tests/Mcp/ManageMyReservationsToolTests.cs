using EventReservationApp.Mcp.Tools;
using EventReservationApp.Services.Implementations;
using EventReservationApp.Services.Interfaces;
using EventReservationApp.Tests.TestHelpers;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol;
using Xunit;

namespace EventReservationApp.Tests.Mcp;

public class ManageMyReservationsToolTests
{
    private static ManageMyReservationsTool CreateTool(Data.ApplicationDbContext db, ICurrentUser currentUser)
    {
        var reservationService = new ReservationService(db);
        var myReservationsService = new MyReservationsService(reservationService, currentUser);
        return new ManageMyReservationsTool(myReservationsService, NullLogger<ManageMyReservationsTool>.Instance);
    }

    [Fact]
    public async Task List_ReturnsOnlyTheCurrentUsersReservations()
    {
        await using var db = InMemoryDbContextFactory.Create();
        db.AddUser("me");
        db.AddUser("someone-else");
        db.AddEvent(1, name: "Event A");
        db.AddEvent(2, name: "Event B");
        db.AddReservation(1, "me", 1);
        db.AddReservation(2, "someone-else", 2);
        await db.SaveChangesAsync();

        var tool = CreateTool(db, FakeCurrentUser.For("me"));

        var result = await tool.ManageMyReservations(operation: "list");

        Assert.Equal("list", result.Operation);
        Assert.True(result.Success);
        var reservation = Assert.Single(result.Reservations!);
        Assert.Equal("Event A", reservation.EventName);
    }

    [Fact]
    public async Task Reserve_CreatesReservationForCurrentUser()
    {
        await using var db = InMemoryDbContextFactory.Create();
        db.AddUser("me");
        db.AddEvent(1, name: "Event A", capacity: 10);
        await db.SaveChangesAsync();

        var tool = CreateTool(db, FakeCurrentUser.For("me"));

        var result = await tool.ManageMyReservations(operation: "reserve", eventId: 1, notes: "aisle seat");

        Assert.Equal("reserve", result.Operation);
        Assert.True(result.Success);

        var listResult = await tool.ManageMyReservations(operation: "list");
        var reservation = Assert.Single(listResult.Reservations!);
        Assert.Equal(1, reservation.EventId);
        Assert.Equal("aisle seat", reservation.Notes);
    }

    [Fact]
    public async Task Cancel_RemovesTheCallersOwnReservation()
    {
        await using var db = InMemoryDbContextFactory.Create();
        db.AddUser("me");
        db.AddEvent(1, name: "Event A");
        db.AddReservation(42, "me", 1);
        await db.SaveChangesAsync();

        var tool = CreateTool(db, FakeCurrentUser.For("me"));

        var result = await tool.ManageMyReservations(operation: "cancel", reservationId: 42);

        Assert.True(result.Success);
        var listResult = await tool.ManageMyReservations(operation: "list");
        Assert.Empty(listResult.Reservations!);
    }

    [Fact]
    public async Task Reserve_Twice_ForSameEvent_ReturnsControlledDuplicateFailure()
    {
        await using var db = InMemoryDbContextFactory.Create();
        db.AddUser("me");
        db.AddEvent(1, name: "Event A", capacity: 10);
        await db.SaveChangesAsync();

        var tool = CreateTool(db, FakeCurrentUser.For("me"));

        var first = await tool.ManageMyReservations(operation: "reserve", eventId: 1);
        var second = await tool.ManageMyReservations(operation: "reserve", eventId: 1);

        Assert.True(first.Success);
        Assert.False(second.Success);
        Assert.NotNull(second.Message);

        var listResult = await tool.ManageMyReservations(operation: "list");
        Assert.Single(listResult.Reservations!); // duplicate was not created
    }

    [Fact]
    public async Task Reserve_WhenEventIsFull_ReturnsControlledFailure()
    {
        await using var db = InMemoryDbContextFactory.Create();
        db.AddUser("me");
        db.AddUser("other");
        db.AddEvent(1, name: "Tiny Event", capacity: 1);
        db.AddReservation(1, "other", 1);
        await db.SaveChangesAsync();

        var tool = CreateTool(db, FakeCurrentUser.For("me"));

        var result = await tool.ManageMyReservations(operation: "reserve", eventId: 1);

        Assert.False(result.Success);
        Assert.NotNull(result.Message);
    }

    [Fact]
    public async Task Cancel_AnotherUsersReservation_IsRejected_AndLeavesItIntact()
    {
        await using var db = InMemoryDbContextFactory.Create();
        db.AddUser("victim");
        db.AddUser("attacker");
        db.AddEvent(1, name: "Event A");
        db.AddReservation(42, "victim", 1);
        await db.SaveChangesAsync();

        var attackerTool = CreateTool(db, FakeCurrentUser.For("attacker"));

        var result = await attackerTool.ManageMyReservations(operation: "cancel", reservationId: 42);

        Assert.False(result.Success);

        var victimTool = CreateTool(db, FakeCurrentUser.For("victim"));
        var victimReservations = await victimTool.ManageMyReservations(operation: "list");
        Assert.Single(victimReservations.Reservations!); // untouched
    }

    [Theory]
    [InlineData("list")]
    [InlineData("reserve")]
    [InlineData("cancel")]
    public async Task UnauthenticatedCaller_IsRejectedWithControlledError_ForEveryOperation(string operation)
    {
        await using var db = InMemoryDbContextFactory.Create();
        var tool = CreateTool(db, FakeCurrentUser.Anonymous());

        var ex = await Assert.ThrowsAsync<McpException>(
            () => tool.ManageMyReservations(operation: operation, eventId: 1, reservationId: 1));

        Assert.DoesNotContain("Exception", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ToolMethod_HasNoUserIdParameter_IdentityCanOnlyComeFromTheAuthenticatedRequest()
    {
        var method = typeof(ManageMyReservationsTool).GetMethod(nameof(ManageMyReservationsTool.ManageMyReservations));
        Assert.NotNull(method);

        var forbiddenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "userid", "user_id", "currentuserid", "callerid", "onbehalfof", "username", "role", "isadministrator"
        };

        foreach (var parameter in method!.GetParameters())
        {
            Assert.False(
                forbiddenNames.Contains(parameter.Name ?? string.Empty),
                $"ManageMyReservations must never accept a '{parameter.Name}' parameter - identity comes only from the authenticated request.");
        }
    }

    [Fact]
    public async Task InvalidOperation_IsRejectedWithControlledError()
    {
        await using var db = InMemoryDbContextFactory.Create();
        db.AddUser("me");
        await db.SaveChangesAsync();

        var tool = CreateTool(db, FakeCurrentUser.For("me"));

        var ex = await Assert.ThrowsAsync<McpException>(() => tool.ManageMyReservations(operation: "delete-everything"));

        Assert.Contains("delete-everything", ex.Message);
    }

    [Fact]
    public async Task Reserve_WithoutEventId_IsRejectedWithControlledError()
    {
        await using var db = InMemoryDbContextFactory.Create();
        db.AddUser("me");
        await db.SaveChangesAsync();

        var tool = CreateTool(db, FakeCurrentUser.For("me"));

        await Assert.ThrowsAsync<McpException>(() => tool.ManageMyReservations(operation: "reserve"));
    }
}
