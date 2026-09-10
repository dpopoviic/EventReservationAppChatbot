using EventReservationApp.Services.Interfaces;

namespace EventReservationApp.Tests.TestHelpers;

/// <summary>
/// Test double for <see cref="ICurrentUser"/>. Unlike the real
/// <c>CurrentUser</c> (which can only ever reflect the authenticated
/// HttpContext), this fake lets a test directly set the simulated
/// identity. That is exactly what makes the security tests meaningful:
/// they prove that <c>MyReservationsService</c>/<c>EventCatalogService</c>
/// only ever act on whatever <see cref="ICurrentUser"/> reports - there is
/// no method parameter anywhere a "model" could use to override it.
/// </summary>
public class FakeCurrentUser : ICurrentUser
{
    public string? UserId { get; set; }
    public bool IsAuthenticated { get; set; }
    public bool IsAdministrator { get; set; }

    public static FakeCurrentUser Anonymous() => new() { IsAuthenticated = false, UserId = null };

    public static FakeCurrentUser For(string userId, bool isAdministrator = false) =>
        new() { IsAuthenticated = true, UserId = userId, IsAdministrator = isAdministrator };

    public string RequireUserId() =>
        UserId ?? throw new UnauthorizedAccessException("This operation requires an authenticated user.");
}
