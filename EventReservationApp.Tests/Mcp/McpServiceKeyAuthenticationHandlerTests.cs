using System.Security.Claims;
using System.Text.Encodings.Web;
using EventReservationApp.Mcp.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace EventReservationApp.Tests.Mcp;

/// <summary>
/// Unit tests for the Phase-1 MCP service-key authentication scheme
/// (<see cref="McpServiceKeyAuthenticationHandler"/>). These exercise the
/// handler directly (the same technique ASP.NET Core's own handlers are unit
/// tested with) since the app's Program.cs requires a real SQL Server
/// connection at startup and isn't wired up for WebApplicationFactory-style
/// integration tests.
/// </summary>
public class McpServiceKeyAuthenticationHandlerTests
{
    private const string ConfiguredKey = "correct-service-key";

    private static async Task<AuthenticateResult> AuthenticateAsync(
        string? headerValue,
        string? configuredKey = ConfiguredKey,
        string? environmentName = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["McpSettings:ServiceKey"] = configuredKey,
            })
            .Build();

        var handler = new McpServiceKeyAuthenticationHandler(
            new StaticOptionsMonitor<McpServiceKeyAuthenticationOptions>(new McpServiceKeyAuthenticationOptions()),
            NullLoggerFactory.Instance,
            UrlEncoder.Default,
            configuration,
            new FakeHostEnvironment(environmentName ?? Environments.Production));

        var scheme = new AuthenticationScheme(
            McpServiceKeyDefaults.AuthenticationScheme,
            McpServiceKeyDefaults.AuthenticationScheme,
            typeof(McpServiceKeyAuthenticationHandler));

        var httpContext = new DefaultHttpContext();
        if (headerValue is not null)
        {
            httpContext.Request.Headers[McpServiceKeyDefaults.HeaderName] = headerValue;
        }

        await handler.InitializeAsync(scheme, httpContext);
        return await handler.AuthenticateAsync();
    }

    [Fact]
    public async Task CorrectServiceKey_IsAccepted()
    {
        var result = await AuthenticateAsync(headerValue: ConfiguredKey);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task CorrectServiceKey_ProducesAPrincipalWithNoUserIdOrNameClaim()
    {
        // This is the invariant the whole Phase-1 design leans on: a service-key
        // caller must never look like a specific signed-in user, so that
        // ICurrentUser.RequireUserId() (and therefore ManageMyReservationsTool)
        // keeps rejecting it with no extra guard code required.
        var result = await AuthenticateAsync(headerValue: ConfiguredKey);

        Assert.True(result.Succeeded);
        var principal = result.Principal!;
        Assert.True(principal.Identity!.IsAuthenticated);
        Assert.Null(principal.FindFirst(ClaimTypes.NameIdentifier));
        Assert.Null(principal.FindFirst(ClaimTypes.Name));
        Assert.Null(principal.Identity.Name);
    }

    [Fact]
    public async Task WrongServiceKey_IsRejected()
    {
        var result = await AuthenticateAsync(headerValue: "wrong-key");

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task MissingHeader_ProducesNoResult_SoOtherSchemesCanStillSucceed()
    {
        var result = await AuthenticateAsync(headerValue: null);

        Assert.False(result.Succeeded);
        Assert.True(result.None);
    }

    [Fact]
    public async Task UnconfiguredServiceKey_OutsideDevelopment_FailsClosed_EvenWithTheRightLookingHeader()
    {
        var result = await AuthenticateAsync(
            headerValue: "anything",
            configuredKey: null,
            environmentName: Environments.Production);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task UnconfiguredServiceKey_InDevelopment_StillFailsClosed()
    {
        var result = await AuthenticateAsync(
            headerValue: "anything",
            configuredKey: "",
            environmentName: Environments.Development);

        Assert.False(result.Succeeded);
    }

    private sealed class StaticOptionsMonitor<T> : IOptionsMonitor<T>
    {
        public StaticOptionsMonitor(T currentValue) => CurrentValue = currentValue;

        public T CurrentValue { get; }

        public T Get(string? name) => CurrentValue;

        public IDisposable OnChange(Action<T, string> listener) => NullDisposable.Instance;

        private sealed class NullDisposable : IDisposable
        {
            public static readonly NullDisposable Instance = new();
            public void Dispose() { }
        }
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public FakeHostEnvironment(string environmentName) => EnvironmentName = environmentName;

        public string EnvironmentName { get; set; }
        public string ApplicationName { get; set; } = "EventReservationApp.Tests";
        public string ContentRootPath { get; set; } = ".";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
