# Event Reservation App

A reference / base **ASP.NET Core MVC** application intended as a starting point for future
projects, especially ones that will later add AI-powered features (a chatbot placeholder is
already wired up for that purpose).

> **Note on this delivery:** This project was generated in an environment without the .NET SDK
> or access to NuGet, so it has **not** been compiled or run here. The code follows standard,
> well-established ASP.NET Core 9 / EF Core 9 / Identity patterns, but please run
> `dotnet restore` and `dotnet build` locally as your first step, and fix up any small
> package-version mismatches your local SDK reports (see "Getting Started" below).

## 1. Project Purpose

This is a clean, understandable **template** you can copy/extend for future applications. It
demonstrates:

- ASP.NET Core MVC with a conventional folder structure
- EF Core + SQL Server with explicit relationship configuration
- ASP.NET Core Identity for authentication, with role-based authorization
- A service layer that keeps business logic out of controllers
- A chatbot page structured so a real AI service can be dropped in later without restructuring
  the app

It intentionally does **not** implement any AI functionality yet - that is the next step, and the
codebase is structured to make that easy (see section 12).

## 2. Technologies Used

| Concern | Technology |
|---|---|
| Framework | .NET 9 / ASP.NET Core MVC |
| ORM | Entity Framework Core 9 |
| Database | SQL Server (local, via SSMS or `sqlcmd`) |
| Auth | ASP.NET Core Identity (default scaffolded Identity UI, Razor Pages) |
| Views | Razor Views (MVC) + Bootstrap 5 (via CDN) |
| DI | Built-in ASP.NET Core Dependency Injection |

## 3. Project Structure

```
EventReservationApp.sln
src/EventReservationApp/
  Controllers/          MVC controllers (thin - delegate to Services)
  Services/
    Interfaces/          Abstractions for business logic (IEventService, IChatbotService, ...)
    Implementations/      Concrete implementations
  Models/
    Entities/             EF Core entities (ApplicationUser, Event, EventReservation)
    ViewModels/            Models shaped for the views (never expose entities directly to forms)
  Data/
    ApplicationDbContext.cs  EF Core DbContext (inherits IdentityDbContext)
    DbInitializer.cs         Seeds roles/users/events/reservations on startup
  Views/                 Razor views, organized per-controller + Shared/
  Migrations/             EF Core migrations (create your first one - see below)
  wwwroot/                Static files (css/js). Bootstrap/jQuery are loaded from CDN in _Layout.cshtml.
  Program.cs               App startup: DI registrations + middleware pipeline
  appsettings.json         Configuration, including the DB connection string
```

Identity's Register/Login/Logout pages come from the **default Identity UI** shipped inside the
`Microsoft.AspNetCore.Identity.UI` package (registered via `AddDefaultIdentity` in `Program.cs`).
There are no physical Razor Pages for this in the project - if you want to customize their look
later, scaffold them with:

```bash
dotnet tool install -g dotnet-aspnet-codegenerator
dotnet aspnet-codegenerator identity -dc EventReservationApp.Data.ApplicationDbContext
```

## 4. Configuring the Database

Open `src/EventReservationApp/appsettings.json` (and/or `appsettings.Development.json`) and edit
the `ConnectionStrings:DefaultConnection` value to point at your local SQL Server instance, e.g.:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=EventReservationAppDb;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True"
}
```

Use SQL Server Management Studio (or Azure Data Studio) to inspect the database once it has been
created by the steps below.

## 5. Creating the Initial EF Core Migration

No migration has been checked in yet - generate the first one locally once you have the SDK and
NuGet packages restored:

```bash
cd src/EventReservationApp
dotnet tool install --global dotnet-ef   # if you don't already have it
dotnet restore
dotnet ef migrations add InitialCreate
```

This inspects `ApplicationDbContext` (Identity tables + `Events` + `EventReservations`) and writes
the migration files into the `Migrations/` folder.

## 6. Creating / Updating the Database

You have two options:

- **Automatic (default):** The app calls `context.Database.MigrateAsync()` on startup
  (`DbInitializer.SeedAsync`, invoked from `Program.cs`), so simply running the app after step 5
  will create/update the database and seed example data.
- **Manual:** Run migrations yourself whenever you prefer:
  ```bash
  dotnet ef database update
  ```

## 7. Running the Application

```bash
cd src/EventReservationApp
dotnet restore
dotnet run
```

Then browse to the URL shown in the console (typically `https://localhost:5001` or similar).

## 8. Example User Accounts

Seeded automatically on first run by `Data/DbInitializer.cs`:

| Role | Email | Password |
|---|---|---|
| Administrator | `admin@example.com` | `Admin123!` |
| Customer | `customer@example.com` | `Customer123!` |

These are **local development credentials only** - change or remove the seeding logic before
using this template for anything beyond local development. Passwords are never hard-coded into
application logic in an unsafe way; they are passed once into Identity's
`UserManager.CreateAsync`, which performs the actual hashing.

## 9. Roles and Permissions

Two roles are seeded: **Administrator** and **Customer**.

| Capability | Administrator | Customer |
|---|:---:|:---:|
| Browse events | ✅ | ✅ (also available to anonymous visitors) |
| Create / edit / delete events | ✅ | ❌ |
| Create a reservation | ✅ | ✅ |
| View / cancel **own** reservations | ✅ | ✅ |
| View / manage **all** reservations | ✅ | ❌ |
| Manage users (edit profile/role, delete) | ✅ | ❌ |
| Manage roles | ✅ | ❌ |

Authorization is enforced with `[Authorize]` / `[Authorize(Roles = "Administrator")]` directly on
controller actions - not just hidden in the UI. For example, `ReservationsController.Delete`
checks that the current user either owns the reservation or is an Administrator before deleting
it, regardless of what the UI shows.

## 10. Main Entities and Relationships

- **ApplicationUser** (extends `IdentityUser`) - adds `FirstName`/`LastName`. All authentication
  fields (username, email, password hash, etc.) are managed by Identity.
- **Event** - `Name`, `Description`, `Location`, `StartDate`, `EndDate`, `Capacity`.
- **EventReservation** - links one `ApplicationUser` to one `Event`, with a `ReservationDate` and
  optional `Notes`.
  - One `ApplicationUser` → many `EventReservation` (`Restrict` on delete: a user with existing
    reservations cannot be deleted until those reservations are removed).
  - One `Event` → many `EventReservation` (`Cascade` on delete: deleting an event removes its
    reservations).
  - A unique index on `(UserId, EventId)` prevents a user from reserving the same event twice.
  - Reservation creation is blocked once `Event.Capacity` is reached (enforced in
    `ReservationService.CreateAsync`, not just in the UI).

These relationships are configured explicitly in `ApplicationDbContext.OnModelCreating`.

## 11. Authentication / Authorization Setup

- **Authentication**: ASP.NET Core Identity (`AddDefaultIdentity<ApplicationUser>()` +
  `AddRoles<IdentityRole>()` + `AddEntityFrameworkStores<ApplicationDbContext>()`), using the
  default scaffolded Identity UI for Register/Login/Logout screens.
- **Authorization**: role-based, using `[Authorize]` and `[Authorize(Roles = "...")]` on
  controllers/actions. `Program.cs` calls `app.UseAuthentication()` then `app.UseAuthorization()`
  in the correct order in the middleware pipeline.
- Server-side enforcement is treated as the source of truth; views additionally hide
  administration links from non-administrators purely for a cleaner UX (see `_Layout.cshtml` and
  `_LoginPartial.cshtml`), but this is not relied upon for security.

## 12. Where to Add Future AI / Chatbot Integration

The chatbot page (`/Chatbot`) is intentionally isolated so AI can be added later without touching
the rest of the MVC application:

- `Services/Interfaces/IChatbotService.cs` - the abstraction the controller and future services
  depend on. **This is the extension point.**
- `Services/Implementations/PlaceholderChatbotService.cs` - the current no-AI implementation
  (returns canned replies). Replace or add a sibling implementation (e.g.
  `OpenAiChatbotService`, `ClaudeChatbotService`) that calls a real AI provider.
- `Controllers/ChatbotController.cs` - serves the chat page and a `POST /Chatbot/Send` JSON
  endpoint; it only talks to `IChatbotService`, never to a concrete AI SDK directly.
- `Views/Chatbot/Index.cshtml` - the chat window UI (message list, input box, send button) that
  calls `POST /Chatbot/Send` via `fetch()`.

To integrate a real AI provider later:

1. Implement `IChatbotService` in a new class, calling your AI provider's SDK/API from
   `GetReplyAsync`.
2. Add any provider configuration (API keys, model name, etc.) under a new section in
   `appsettings.json` (a `ChatbotSettings` placeholder section already exists) and bind it with
   `IOptions<T>`.
3. In `Program.cs`, change the DI registration from:
   ```csharp
   builder.Services.AddScoped<IChatbotService, PlaceholderChatbotService>();
   ```
   to your new implementation. No controller or view changes are required.
4. If you need conversation history/context, consider adding a `ChatSession`/`ChatMessage`
   entity and persisting it via EF Core, following the same entity/service/viewmodel pattern
   used for `Event`/`EventReservation` elsewhere in this project.

## 13. Notable Design Decisions

- **Default Identity UI** was used for Register/Login/Logout (rather than fully custom MVC
  controllers/views) to minimize custom authentication code and stay aligned with Microsoft's
  supported, regularly-updated implementation. Scaffold it (see section 3) if you want to
  customize its appearance later.
- **No Areas** - Administrator and Customer functionality live in the same set of controllers,
  distinguished purely by `[Authorize(Roles = "...")]` attributes, to keep the project structure
  simple for a template. If the application grows significantly, consider introducing an
  `Areas/Admin` area.
- **Capacity enforcement** - reservations are blocked server-side once an event's `Capacity` is
  reached (`ReservationService.CreateAsync`), not just visually disabled in the UI.
- **Service layer** - all business logic (capacity checks, uniqueness checks, role assignment,
  etc.) lives in `Services/Implementations`, not in controllers, so it can be unit tested and
  reused independently of MVC.
- Styling uses Bootstrap 5 loaded from a CDN to avoid requiring a Node/npm/LibMan toolchain for
  a "base" project - swap this for a local copy or a build pipeline as needed.

## 14. Known Limitations / Next Steps

- No automated tests are included (kept out to avoid over-engineering a template project) -
  consider adding a test project (xUnit + an in-memory or SQLite EF Core provider) as a next
  step.
- The chatbot placeholder has no memory/persistence across requests; add a `ChatMessage` entity
  if you want conversation history.
- `EventReservation.User`/`Event` navigation properties are nullable-annotated but always
  populated by the service layer's `Include()` calls - if you query the DbContext directly
  elsewhere, remember to include the related entities.
