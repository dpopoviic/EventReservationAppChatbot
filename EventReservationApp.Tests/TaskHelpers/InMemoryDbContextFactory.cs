using EventReservationApp.Data;
using Microsoft.EntityFrameworkCore;

namespace EventReservationApp.Tests.TestHelpers;

/// <summary>
/// Creates a fresh, isolated in-memory <see cref="ApplicationDbContext"/>
/// for a single test. A new Guid-named database per call guarantees tests
/// never see each other's data, even when the test runner executes them
/// in parallel.
/// </summary>
public static class InMemoryDbContextFactory
{
    public static ApplicationDbContext Create()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new ApplicationDbContext(options);
    }
}
