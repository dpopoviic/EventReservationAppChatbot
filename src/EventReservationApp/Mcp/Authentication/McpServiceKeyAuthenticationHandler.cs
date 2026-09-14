using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace EventReservationApp.Mcp.Authentication;

/// <summary>
/// Scheme name/header constants for <see cref="McpServiceKeyAuthenticationHandler"/>,
/// mirroring the "XyzDefaults" convention used by the built-in ASP.NET Core
/// authentication handlers (e.g. <c>JwtBearerDefaults</c>).
/// </summary>
public static class McpServiceKeyDefaults
{
    public const string AuthenticationScheme = "McpServiceKey";
    public const string HeaderName = "X-Mcp-Service-Key";
}

public sealed class McpServiceKeyAuthenticationOptions : AuthenticationSchemeOptions
{
}

/// <summary>
/// Phase-1, temporary authentication path for the MCP endpoint: lets Foundry
/// Agent Service call /mcp with a single static credential (an
/// "X-Mcp-Service-Key" header checked against configuration key
/// "McpSettings:ServiceKey") ahead of building real per-user OAuth. This is
/// registered as an additional accepted scheme on the "Mcp" authorization
/// policy alongside <see cref="Microsoft.AspNetCore.Identity.IdentityConstants.BearerScheme"/>
/// - see Program.cs.
///
/// Deliberately does NOT resolve to any particular user: the resulting
/// ClaimsPrincipal carries no name/user-id claim, only a generic "service"
/// identity. That is what makes ICurrentUser.RequireUserId() keep throwing
/// UnauthorizedAccessException for requests authenticated this way, which in
/// turn is what naturally stops ManageMyReservationsTool from being usable
/// via the service key alone - no extra guard code is needed inside the tool
/// itself. A real per-user OAuth flow (see POST /mcp/token) is required
/// before an MCP client can act on behalf of a specific user.
/// </summary>
public sealed class McpServiceKeyAuthenticationHandler : AuthenticationHandler<McpServiceKeyAuthenticationOptions>
{
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;

    public McpServiceKeyAuthenticationHandler(
        IOptionsMonitor<McpServiceKeyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration configuration,
        IHostEnvironment environment)
        : base(options, logger, encoder)
    {
        _configuration = configuration;
        _environment = environment;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var configuredKey = _configuration["McpSettings:ServiceKey"];

        // Fail closed: an unset/empty secret must never be treated as "no
        // restriction". Outside Development this is a hard misconfiguration and
        // every request is rejected; the placeholder shipped in
        // appsettings.json/appsettings.Development.json is intentionally empty,
        // with the real value coming from user-secrets or Key Vault.
        if (string.IsNullOrEmpty(configuredKey))
        {
            if (!_environment.IsDevelopment())
            {
                Logger.LogError(
                    "McpSettings:ServiceKey is not configured; rejecting all {Scheme} authentication attempts.",
                    Scheme.Name);
            }

            return Task.FromResult(AuthenticateResult.Fail("Service key authentication is not configured."));
        }

        if (!Request.Headers.TryGetValue(McpServiceKeyDefaults.HeaderName, out var providedValues))
        {
            // No credential presented for this scheme - let the other scheme(s) on
            // the policy (e.g. the bearer-token scheme) have a chance to succeed.
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var providedKey = providedValues.ToString();
        if (!FixedTimeEquals(providedKey, configuredKey))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid service key."));
        }

        // No ClaimTypes.NameIdentifier/Name (or any other user-identifying claim)
        // is added here on purpose - see the class-level remarks above.
        var identity = new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.AuthenticationMethod, McpServiceKeyDefaults.AuthenticationScheme) },
            authenticationType: Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    private static bool FixedTimeEquals(string provided, string expected)
    {
        var providedBytes = Encoding.UTF8.GetBytes(provided);
        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        if (providedBytes.Length != expectedBytes.Length)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(providedBytes, expectedBytes);
    }
}
