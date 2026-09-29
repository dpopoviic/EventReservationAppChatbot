using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using EventReservationApp.Models.Entities;
using EventReservationApp.Services.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace EventReservationApp.Services.Auth;

// Authenticates calls from Rasa's custom actions to the internal agent API.
// Two independent checks:
//   1. X-Internal-Api-Key - the caller is our Rasa deployment (service identity).
//   2. Authorization: Bearer <user token> - the user the call is made for. The
//      token was issued by UserTokenService for that user's chat message and
//      only forwarded by Rasa, so the user id is never taken from Rasa's word.
public class InternalApiKeyHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "InternalApiKey";

    private const string ApiKeyHeaderName = "X-Internal-Api-Key";
    private const string BearerPrefix = "Bearer ";

    private readonly IConfiguration _configuration;
    private readonly IUserTokenService _userTokenService;
    private readonly UserManager<ApplicationUser> _userManager;

    public InternalApiKeyHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IConfiguration configuration,
        IUserTokenService userTokenService,
        UserManager<ApplicationUser> userManager)
        : base(options, logger, encoder)
    {
        _configuration = configuration;
        _userTokenService = userTokenService;
        _userManager = userManager;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var configuredKey = _configuration["InternalApi:ApiKey"];

        if (string.IsNullOrWhiteSpace(configuredKey))
        {
            return AuthenticateResult.Fail("InternalApi:ApiKey is not configured.");
        }

        if (!Request.Headers.TryGetValue(ApiKeyHeaderName, out var providedKey) ||
            providedKey.Count != 1 ||
            !KeysMatch(providedKey[0], configuredKey))
        {
            return AuthenticateResult.Fail("Invalid or missing API key.");
        }

        var authorization = Request.Headers.Authorization;
        if (authorization.Count != 1 ||
            authorization[0] is not { } header ||
            !header.StartsWith(BearerPrefix, StringComparison.Ordinal))
        {
            return AuthenticateResult.Fail("Missing user token.");
        }

        var userId = await _userTokenService.ValidateTokenAsync(header[BearerPrefix.Length..].Trim());
        if (userId is null)
        {
            return AuthenticateResult.Fail("Invalid or expired user token.");
        }

        // The token can outlive a change to the account (deleted, locked out)
        // by a few minutes, so the account itself is checked on every call.
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return AuthenticateResult.Fail("Unknown user.");
        }

        if (await _userManager.IsLockedOutAsync(user))
        {
            return AuthenticateResult.Fail("User is locked out.");
        }

        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, user.Id) };
        claims.AddRange((await _userManager.GetRolesAsync(user)).Select(role => new Claim(ClaimTypes.Role, role)));

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);

        return AuthenticateResult.Success(ticket);
    }

    // Constant-time comparison so response timing does not reveal how much of the key matched.
    private static bool KeysMatch(string? provided, string configured) =>
        provided is not null &&
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(provided),
            Encoding.UTF8.GetBytes(configured));
}
