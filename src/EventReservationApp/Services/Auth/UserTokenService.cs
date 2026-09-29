using System.Security.Claims;
using System.Security.Cryptography;
using EventReservationApp.Services.Interfaces;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace EventReservationApp.Services.Auth;

public class UserTokenService : IUserTokenService
{
    // Must match USER_TOKEN_ISSUER / USER_TOKEN_AUDIENCE in the Rasa project
    // (secure_rest_channel.py).
    public const string Issuer = "event-reservation-app";
    public const string Audience = "event-reservation-chatbot";

    // One token per chat message. Short, because the token is stored in Rasa's
    // tracker store with the message metadata and must not stay usable there.
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private readonly JsonWebTokenHandler _tokenHandler = new();
    private readonly SigningCredentials _signingCredentials;
    private readonly TokenValidationParameters _validationParameters;

    public UserTokenService(IConfiguration configuration)
    {
        var privateKeyPem = configuration["UserToken:PrivateKeyPem"];
        if (string.IsNullOrWhiteSpace(privateKeyPem))
        {
            throw new InvalidOperationException("UserToken:PrivateKeyPem is not configured.");
        }

        var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(privateKeyPem);
        if (ecdsa.KeySize != 256)
        {
            throw new InvalidOperationException("UserToken:PrivateKeyPem must be an EC P-256 private key.");
        }

        var key = new ECDsaSecurityKey(ecdsa);
        _signingCredentials = new SigningCredentials(key, SecurityAlgorithms.EcdsaSha256);

        _validationParameters = new TokenValidationParameters
        {
            ValidIssuer = Issuer,
            ValidAudience = Audience,
            IssuerSigningKey = key,
            ValidAlgorithms = new[] { SecurityAlgorithms.EcdsaSha256 },
            RequireSignedTokens = true,
            RequireExpirationTime = true,
            ClockSkew = TimeSpan.FromSeconds(30)
        };
    }

    public string CreateToken(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("User id is required.", nameof(userId));
        }

        var now = DateTime.UtcNow;

        return _tokenHandler.CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[] { new Claim(JwtRegisteredClaimNames.Sub, userId) }),
            Issuer = Issuer,
            Audience = Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.Add(Lifetime),
            SigningCredentials = _signingCredentials
        });
    }

    public async Task<string?> ValidateTokenAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var result = await _tokenHandler.ValidateTokenAsync(token, _validationParameters);
        if (!result.IsValid || result.SecurityToken is not JsonWebToken jwt)
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(jwt.Subject) ? null : jwt.Subject;
    }
}
