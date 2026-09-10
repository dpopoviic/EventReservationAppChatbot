using System.Reflection;
using EventReservationApp.Services.Implementations;
using EventReservationApp.Services.Interfaces;
using EventReservationApp.Tests.TestHelpers;
using Xunit;

namespace EventReservationApp.Tests;

/// <summary>
/// Cross-cutting security tests for the future MCP surface, targeting the
/// specific boundaries called out in the task:
///   - the model/caller can never supply a UserId,
///   - another user's data can never be returned,
///   - another user's reservation can never be cancelled.
///
/// Some scenarios here overlap with <see cref="MyReservationsServiceTests"/>
/// and <see cref="EventCatalogServiceTests"/> by design - they are kept
/// together here as an explicit, easy-to-audit checklist.
/// </summary>
public class SecurityBoundaryTests
{
    private static readonly HashSet<string> ForbiddenParameterNames =
        new(StringComparer.OrdinalIgnoreCase) { "userid", "user_id", "currentuserid", "callerid", "onbehalfof" };

    [Fact]
    public void IMyReservationsService_NeverAcceptsAUserIdParameter()
    {
        AssertNoMethodAcceptsAUserIdParameter(typeof(IMyReservationsService));
    }

    [Fact]
    public void IEventCatalogService_NeverAcceptsAUserIdParameter()
    {
        AssertNoMethodAcceptsAUserIdParameter(typeof(IEventCatalogService));
    }

    private static void AssertNoMethodAcceptsAUserIdParameter(Type serviceInterface)
    {
        foreach (var method in serviceInterface.GetMethods())
        {
            foreach (var parameter in method.GetParameters())
            {
                var name = parameter.Name ?? string.Empty;
                Assert.False(
                    ForbiddenParameterNames.Contains(name),
                    $"{serviceInterface.Name}.{method.Name}({name}) lets a caller supply a user id directly; " +
                    "identity must come from ICurrentUser only.");
            }
        }
    }

    [Fact]
    public async Task AnotherUsersReservations_AreNeverReturned()
    {
        await using var db = InMemoryDbContextFactory.Create();
        db.AddUser("victim");
        db.AddUser("attacker");
        db.AddEvent(1, name: "Private-ish Event");
        db.AddReservation(1, "victim", 1, notes: "victim's private note");
        await db.SaveChangesAsync();

        var reservationService = new ReservationService(db);
        var attackerService = new MyReservationsService(reservationService, FakeCurrentUser.For("attacker"));

        var attackerReservations = await attackerService.GetMyReservationsAsync();

        Assert.Empty(attackerReservations);
    }

    [Fact]
    public async Task AnotherUsersReservation_CannotBeCancelled_EvenByKnowingItsId()
    {
        await using var db = InMemoryDbContextFactory.Create();
        db.AddUser("victim");
        db.AddUser("attacker");
        db.AddEvent(1, name: "Event A");
        db.AddReservation(42, "victim", 1);
        await db.SaveChangesAsync();

        var reservationService = new ReservationService(db);
        var attackerService = new MyReservationsService(reservationService, FakeCurrentUser.For("attacker"));

        var result = await attackerService.CancelForCurrentUserAsync(42);

        Assert.False(result.Success);

        var victimService = new MyReservationsService(reservationService, FakeCurrentUser.For("victim"));
        var victimReservations = await victimService.GetMyReservationsAsync();
        Assert.Single(victimReservations); // still there - the attacker's attempt had no effect
    }

    [Fact]
    public async Task CurrentUserHasReservation_NeverTrueForAnonymousCallers_RegardlessOfOthersReservations()
    {
        await using var db = InMemoryDbContextFactory.Create();
        db.AddUser("someone");
        db.AddEvent(1, name: "Event A");
        db.AddReservation(1, "someone", 1);
        await db.SaveChangesAsync();

        var catalogService = new EventCatalogService(db, FakeCurrentUser.Anonymous());

        var results = await catalogService.SearchEventsAsync(new Models.Dtos.EventSearchFilter());

        Assert.All(results, r => Assert.False(r.CurrentUserHasReservation));
    }
}
