using EventReservationApp.Data;
using EventReservationApp.Models.Entities;
using EventReservationApp.Services.AgentTools;
using EventReservationApp.Services.Implementations;
using EventReservationApp.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

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

// Plain agent-tool wrapper classes (no MCP attributes) whose [Description]
// attributes are read by AIFunctionFactory.Create to build tool schemas for
// the Responses agent. See Services/AgentTools/.
builder.Services.AddScoped<SearchEventsAgentTool>();
builder.Services.AddScoped<GetEventAvailabilityAgentTool>();
builder.Services.AddScoped<ManageMyReservationsAgentTool>();
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
// Seed database (roles, example users, events, reservations)
// ---------------------------------------------------------------------
using (var scope = app.Services.CreateScope())
{
    await DbInitializer.SeedAsync(scope.ServiceProvider);
}

app.Run();
