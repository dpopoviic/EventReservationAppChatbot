using EventReservationApp.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace EventReservationApp.Data;

/// <summary>
/// Applies pending migrations and seeds the database with example data:
/// roles, an administrator, a customer, sample events and reservations.
///
/// Credentials used here are for LOCAL DEVELOPMENT ONLY. See README.md.
/// Passwords are never hard-coded into application logic - they are only
/// passed once, here, into Identity's own UserManager.CreateAsync, which
/// performs the proper hashing.
/// </summary>
public static class DbInitializer
{
    public const string AdminRole = "Administrator";
    public const string CustomerRole = "Customer";

    private const string AdminEmail = "admin@example.com";
    private const string AdminPassword = "Admin123!";

    private const string CustomerEmail = "customer@example.com";
    private const string CustomerPassword = "Customer123!";

    public static async Task SeedAsync(IServiceProvider services)
    {
        var context = services.GetRequiredService<ApplicationDbContext>();

        // Applies any pending EF Core migrations automatically on startup.
        // Remove this call if you prefer to run `dotnet ef database update`
        // manually as part of your deployment process instead.
        await context.Database.MigrateAsync();

        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        await SeedRolesAsync(roleManager);
        var admin = await SeedUserAsync(userManager, AdminEmail, AdminPassword, "System", "Administrator", AdminRole);
        var customer = await SeedUserAsync(userManager, CustomerEmail, CustomerPassword, "Sample", "Customer", CustomerRole);

        await SeedEventsAndReservationsAsync(context, admin, customer);
    }

    private static async Task SeedRolesAsync(RoleManager<IdentityRole> roleManager)
    {
        foreach (var role in new[] { AdminRole, CustomerRole })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }
    }

    private static async Task<ApplicationUser> SeedUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string password,
        string firstName,
        string lastName,
        string role)
    {
        var existing = await userManager.FindByEmailAsync(email);
        if (existing is not null)
        {
            return existing;
        }

        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FirstName = firstName,
            LastName = lastName
        };

        var result = await userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            var errors = string.Join("; ", result.Errors.Select(e => e.Description));
            throw new InvalidOperationException($"Failed to seed user '{email}': {errors}");
        }

        await userManager.AddToRoleAsync(user, role);
        return user;
    }

    private static async Task SeedEventsAndReservationsAsync(
        ApplicationDbContext context,
        ApplicationUser admin,
        ApplicationUser customer)
    {
        if (await context.Events.AnyAsync())
        {
            return; // already seeded
        }

        var events = new List<Event>
        {
            new()
            {
                Name = "ASP.NET Core Conference 2026",
                Description = "A full-day conference covering the latest in ASP.NET Core, EF Core and cloud-native .NET development.",
                Location = "Belgrade Congress Centre",
                StartDate = DateTime.UtcNow.AddDays(14),
                EndDate = DateTime.UtcNow.AddDays(14).AddHours(8),
                Capacity = 150
            },
            new()
            {
                Name = "Intro to AI-Powered Applications",
                Description = "A hands-on workshop about integrating AI assistants into everyday business applications.",
                Location = "Novi Sad Tech Hub",
                StartDate = DateTime.UtcNow.AddDays(21),
                EndDate = DateTime.UtcNow.AddDays(21).AddHours(4),
                Capacity = 40
            },
            new()
            {
                Name = "Local Developer Meetup",
                Description = "Casual monthly meetup for local developers to share what they're building.",
                Location = "Kragujevac Innovation Center",
                StartDate = DateTime.UtcNow.AddDays(7),
                EndDate = DateTime.UtcNow.AddDays(7).AddHours(2),
                Capacity = 30
            },
            new()
            {
                Name = "Cloud Architecture Summit",
                Description = "Deep dive into scalable cloud architecture patterns for modern web applications.",
                Location = "Niš Business Center",
                StartDate = DateTime.UtcNow.AddDays(30),
                EndDate = DateTime.UtcNow.AddDays(30).AddHours(6),
                Capacity = 100
            }
        };

        context.Events.AddRange(events);
        await context.SaveChangesAsync();

        var reservations = new List<EventReservation>
        {
            new()
            {
                UserId = customer.Id,
                EventId = events[0].Id,
                ReservationDate = DateTime.UtcNow.AddDays(-2),
                Notes = "Looking forward to the keynote."
            },
            new()
            {
                UserId = customer.Id,
                EventId = events[1].Id,
                ReservationDate = DateTime.UtcNow.AddDays(-1)
            },
            new()
            {
                UserId = admin.Id,
                EventId = events[2].Id,
                ReservationDate = DateTime.UtcNow
            }
        };

        context.EventReservations.AddRange(reservations);
        await context.SaveChangesAsync();
    }
}
