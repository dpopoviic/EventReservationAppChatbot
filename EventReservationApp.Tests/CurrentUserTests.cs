using System.Security.Claims;
using EventReservationApp.Services.Implementations;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace EventReservationApp.Tests;

/// <summary>
/// Tests the real <see cref="CurrentUser"/> implementation (not the fake)
/// to prove identity is derived exclusively from the authenticated
/// HttpContext's claims - there is no other input it reads.
/// </summary>
public class CurrentUserTests
{
    private static CurrentUser CreateWithPrincipal(ClaimsPrincipal principal)
    {
        var httpContext = new DefaultHttpContext { User = principal };
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        return new CurrentUser(accessor);
    }

    private static ClaimsPrincipal AuthenticatedPrincipal(string userId, params string[] roles)
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId) };
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var identity = new ClaimsIdentity(claims, authenticationType: "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public void UserId_ForAuthenticatedUser_ReflectsNameIdentifierClaim()
    {
        var currentUser = CreateWithPrincipal(AuthenticatedPrincipal("user-123"));

        Assert.True(currentUser.IsAuthenticated);
        Assert.Equal("user-123", currentUser.UserId);
    }

    [Fact]
    public void IsAdministrator_ReflectsAdministratorRoleClaim()
    {
        var adminUser = CreateWithPrincipal(AuthenticatedPrincipal("admin-1", "Administrator"));
        var regularUser = CreateWithPrincipal(AuthenticatedPrincipal("user-1"));

        Assert.True(adminUser.IsAdministrator);
        Assert.False(regularUser.IsAdministrator);
    }

    [Fact]
    public void UserId_ForUnauthenticatedRequest_IsNull()
    {
        // An empty ClaimsIdentity with no authenticationType is "not authenticated".
        var anonymousPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
        var currentUser = CreateWithPrincipal(anonymousPrincipal);

        Assert.False(currentUser.IsAuthenticated);
        Assert.Null(currentUser.UserId);
        Assert.False(currentUser.IsAdministrator);
    }

    [Fact]
    public void UserId_WhenNoHttpContextIsAvailable_IsNull()
    {
        var accessor = new HttpContextAccessor { HttpContext = null };
        var currentUser = new CurrentUser(accessor);

        Assert.False(currentUser.IsAuthenticated);
        Assert.Null(currentUser.UserId);
    }

    [Fact]
    public void RequireUserId_WhenAuthenticated_ReturnsUserId()
    {
        var currentUser = CreateWithPrincipal(AuthenticatedPrincipal("user-123"));

        Assert.Equal("user-123", currentUser.RequireUserId());
    }

    [Fact]
    public void RequireUserId_WhenNotAuthenticated_Throws()
    {
        var anonymousPrincipal = new ClaimsPrincipal(new ClaimsIdentity());
        var currentUser = CreateWithPrincipal(anonymousPrincipal);

        Assert.Throws<UnauthorizedAccessException>(() => currentUser.RequireUserId());
    }
}
