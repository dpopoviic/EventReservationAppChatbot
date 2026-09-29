using System.Security.Claims;
using System.Security.Cryptography;
using EventReservationApp.Services.Auth;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace EventReservationApp.Tests;

/// <summary>
/// The user token is the only thing that tells the internal agent API which
/// user a Rasa action is acting for, so only tokens this app signed, for this
/// purpose and still within their lifetime, may yield a user id.
/// </summary>
public class UserTokenServiceTests
{
    private static string NewPrivateKeyPem()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        return ecdsa.ExportPkcs8PrivateKeyPem();
    }

    private static UserTokenService CreateService(string privateKeyPem)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["UserToken:PrivateKeyPem"] = privateKeyPem
            })
            .Build();

        return new UserTokenService(configuration);
    }

    private static string SignToken(
        string privateKeyPem,
        string subject,
        string issuer = UserTokenService.Issuer,
        string audience = UserTokenService.Audience,
        DateTime? expires = null)
    {
        var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(privateKeyPem);
        var expiresAt = expires ?? DateTime.UtcNow.AddMinutes(5);

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[] { new Claim(JwtRegisteredClaimNames.Sub, subject) }),
            Issuer = issuer,
            Audience = audience,
            IssuedAt = expiresAt.AddMinutes(-10),
            NotBefore = expiresAt.AddMinutes(-10),
            Expires = expiresAt,
            SigningCredentials = new SigningCredentials(new ECDsaSecurityKey(ecdsa), SecurityAlgorithms.EcdsaSha256)
        });
    }

    [Fact]
    public async Task CreatedToken_ValidatesToTheSameUserId()
    {
        var service = CreateService(NewPrivateKeyPem());

        var token = service.CreateToken("user-123");

        Assert.Equal("user-123", await service.ValidateTokenAsync(token));
    }

    [Fact]
    public async Task TokenSignedWithAnotherKey_IsRejected()
    {
        var service = CreateService(NewPrivateKeyPem());

        var forged = SignToken(NewPrivateKeyPem(), "victim");

        Assert.Null(await service.ValidateTokenAsync(forged));
    }

    [Fact]
    public async Task TamperedToken_IsRejected()
    {
        var key = NewPrivateKeyPem();
        var service = CreateService(key);
        var token = service.CreateToken("attacker");

        // Swap the payload for one naming another user, keeping the original signature.
        var parts = token.Split('.');
        var victimPayload = Base64UrlEncoder.Encode(
            Base64UrlEncoder.Decode(parts[1]).Replace("\"attacker\"", "\"victim\""));
        var tampered = $"{parts[0]}.{victimPayload}.{parts[2]}";

        Assert.Null(await service.ValidateTokenAsync(tampered));
    }

    [Fact]
    public async Task ExpiredToken_IsRejected()
    {
        var key = NewPrivateKeyPem();
        var service = CreateService(key);

        var expired = SignToken(key, "user-123", expires: DateTime.UtcNow.AddMinutes(-2));

        Assert.Null(await service.ValidateTokenAsync(expired));
    }

    [Theory]
    [InlineData("some-other-app", UserTokenService.Audience)]
    [InlineData(UserTokenService.Issuer, "some-other-api")]
    public async Task TokenForAnotherIssuerOrAudience_IsRejected(string issuer, string audience)
    {
        var key = NewPrivateKeyPem();
        var service = CreateService(key);

        var token = SignToken(key, "user-123", issuer, audience);

        Assert.Null(await service.ValidateTokenAsync(token));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-jwt")]
    public async Task MalformedToken_IsRejected(string token)
    {
        var service = CreateService(NewPrivateKeyPem());

        Assert.Null(await service.ValidateTokenAsync(token));
    }

    [Fact]
    public void MissingPrivateKey_FailsAtStartup()
    {
        Assert.Throws<InvalidOperationException>(() => CreateService(""));
    }
}
