using EventReservationApp.Data;
using EventReservationApp.Mcp.Authentication;
using EventReservationApp.Mcp.Tools;
using EventReservationApp.Models.Entities;
using EventReservationApp.Services.Implementations;
using EventReservationApp.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------
// Database
// ---------------------------------------------------------------------
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found in appsettings.json.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// ---------------------------------------------------------------------
// ASP.NET Core Identity
// ---------------------------------------------------------------------
// Uses the default Identity UI (scaffolded Razor Pages shipped inside the
// Microsoft.AspNetCore.Identity.UI package) for Register/Login/Logout/etc.
// Run `dotnet aspnet-codegenerator identity` if you ever want to scaffold
// and customize the physical Identity Razor Pages into this project.
builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
    {
        // Standard password/account options - adjust as needed.
        options.SignIn.RequireConfirmedAccount = false;
        options.Password.RequiredLength = 6;
        options.Password.RequireNonAlphanumeric = false;
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Identity/Account/Login";
    options.AccessDeniedPath = "/Identity/Account/AccessDenied";
});

// ---------------------------------------------------------------------
// Application services (business logic lives here, not in controllers)
// ---------------------------------------------------------------------
builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<IReservationService, ReservationService>();
builder.Services.AddScoped<IUserManagementService, UserManagementService>();
builder.Services.AddScoped<IRoleManagementService, RoleManagementService>();


builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();

builder.Services.AddScoped<IEventCatalogService, EventCatalogService>();
builder.Services.AddScoped<IMyReservationsService, MyReservationsService>();
// Chatbot service is intentionally isolated behind an interface so the AI
// provider can be swapped without touching the controller or views. See
// Services/Interfaces/IChatbotService.cs.
//
// FoundryChatbotService calls an existing agent you created in the Microsoft
// Foundry portal (with File Search over your uploaded file). It's registered
// as a singleton because it keeps a reusable connection to Foundry plus one
// chat session per user - see the class for details. To go back to the
// no-AI placeholder, swap this line for:
//   builder.Services.AddScoped<IChatbotService, PlaceholderChatbotService>();
builder.Services.AddSingleton<IChatbotService, FoundryChatbotService>();
builder.Services.AddScoped<IChatbotConversationService, FoundryChatbotConversationService>();
// ---------------------------------------------------------------------
// MVC + Razor Pages (Razor Pages are required by the default Identity UI)
// ---------------------------------------------------------------------
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// ---------------------------------------------------------------------
// MCP server (Model Context Protocol) - exposes exactly three tools
// (SearchEvents, ManageMyReservations, GetEventAvailability) that call the
// same application services as the rest of the app. See Mcp/Tools/*.
//
// Identity: the cookie scheme above (from AddDefaultIdentity) authenticates
// the MVC site as before and stays the default scheme, unchanged. The MCP
// endpoint additionally accepts ASP.NET Core Identity's built-in bearer
// token scheme, because a remote MCP client (an agent orchestrator, not a
// browser) cannot carry a login cookie. A bearer token still resolves to
// the same authenticated ApplicationUser/ClaimsPrincipal via the framework's
// own token validation - there is no custom header and no way for a caller
// to supply a user id itself. See POST /mcp/token below for how a
// cookie-authenticated user obtains one of these tokens, and the delivered
// write-up for what changes if/when this is connected to Foundry.
//
// PHASE 1 / TEMPORARY: also accepts a static service credential (an
// "X-Mcp-Service-Key" header, see McpServiceKeyAuthenticationHandler) so
// Foundry Agent Service can call the MCP endpoint with one shared secret
// ahead of real per-user OAuth being wired up. Unlike the bearer scheme,
// this never resolves to a specific user - see the handler for details.
builder.Services.AddAuthentication()
    .AddBearerToken(IdentityConstants.BearerScheme)
    .AddScheme<McpServiceKeyAuthenticationOptions, McpServiceKeyAuthenticationHandler>(
        McpServiceKeyDefaults.AuthenticationScheme, options => { });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Mcp", policy => policy
        .AddAuthenticationSchemes(IdentityConstants.BearerScheme, McpServiceKeyDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser());
});

builder.Services.AddMcpServer()
    .WithHttpTransport(options => options.SessionMode = HttpServerSessionMode.Stateless)
    .WithTools<SearchEventsTool>()
    .WithTools<ManageMyReservationsTool>()
    .WithTools<GetEventAvailabilityTool>();

// The chatbot page posts messages via fetch(); configure the antiforgery
// header name so its JavaScript can send the token explicitly (see
// Views/Chatbot/Index.cshtml).
builder.Services.AddAntiforgery(options => options.HeaderName = "X-CSRF-TOKEN");

var app = builder.Build();

// ---------------------------------------------------------------------
// Middleware pipeline
// ---------------------------------------------------------------------
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

// ---------------------------------------------------------------------
// MCP endpoint - protected by the "Mcp" policy configured above, which
// accepts either the per-user bearer-token scheme or the temporary
// service-key scheme. Never reachable anonymously and never reachable via
// the cookie scheme alone, so a caller cannot get in just by having a
// browser session open.
// ---------------------------------------------------------------------
app.MapMcp("/mcp").RequireAuthorization("Mcp");

// Lets an already browser-authenticated (cookie) user mint a bearer token
// for their OWN identity, to hand to a separate MCP client. The user is
// read from the existing authenticated HttpContext - exactly like every
// other authenticated action in this app - never from anything the caller
// passes in. This is a minimal, self-issued stand-in for a real OAuth
// authorization server; see the delivered write-up for what a genuine
// Foundry "OAuth identity passthrough" connection would additionally
// require.
app.MapPost("/mcp/token", async (
    HttpContext httpContext,
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager) =>
{
    var user = await userManager.GetUserAsync(httpContext.User);
    if (user is null)
    {
        return Results.Unauthorized();
    }

    var principal = await signInManager.CreateUserPrincipalAsync(user);
    return Results.SignIn(principal, authenticationScheme: IdentityConstants.BearerScheme);
})
.RequireAuthorization();

// ---------------------------------------------------------------------
// Seed database (roles, example users, events, reservations)
// ---------------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    await DbInitializer.SeedAsync(scope.ServiceProvider);
}

app.Run();
