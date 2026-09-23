using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace EventReservationApp.Services.Auth;

public class InternalApiKeyHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "InternalApiKey";

    private const string ApiKeyHeaderName = "X-Internal-Api-Key";
    private const string UserIdHeaderName = "X-User-Id";

    private readonly IConfiguration _configuration;

    public InternalApiKeyHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration configuration)
        : base(options, logger, encoder)
    {
        _configuration = configuration;
    }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var configuredKey = _configuration["InternalApi:ApiKey"];

        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            return Task.FromResult(AuthenticateResult.Fail(
                "InternalApi:ApiKey is not configured."));
        }

        if (!Request.Headers.TryGetValue(ApiKeyHeaderName, out var providedKey) ||
            providedKey.Count != 1 ||
            !string.Equals(providedKey[0], configuredKey, StringComparison.Ordinal))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid or missing API key."));
        }

        if (!Request.Headers.TryGetValue(UserIdHeaderName, out var userId) ||
            userId.Count != 1 ||
            string.IsNullOrWhiteSpace(userId[0]))
        {
            return Task.FromResult(AuthenticateResult.Fail("Missing X-User-Id header."));
        }

        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userId[0]!) };
        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return Task.FromResult(AuthenticateResult.Success(ticket));
    }
}
