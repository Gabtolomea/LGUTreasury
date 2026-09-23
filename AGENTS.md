# LGUTreasury

ASP.NET Core MVC app targeting `.NET 10` (`net10.0`), C# nullables + implicit usings enabled. No test project and no lint/format scripts exist — verify changes with `dotnet build`.

## Commands
Run from `LGUTreasury/`:
- `dotnet build` / `dotnet run` are the standard verification loop.
- Migrations under `Migrations/` are applied manually with `dotnet ef database update`. The app itself never runs migrations: `Program.cs` calls `context.Database.EnsureCreated()` on startup instead.

## Database / data layer
- MySQL via `MySql.EntityFrameworkCore` (`options.UseMySQL`). Connection string is `DefaultConnection` in `appsettings.json` (database `LGUTreasury`, localhost, plaintext root password). Keep using the MySQL provider — do not swap to Pomelo.
- Every startup `Program.cs` seeds: default admin `ADMIN001` / `Admin@1234` (BCrypt hashed, only if no `Admin` row exists) plus `RevenueCategories`, `RevenueTypes`, and a `RevenuePolicy` per type.
- Quirk: because the schema comes from `EnsureCreated()`, adding a new entity/DbSet to `Data/ApplicationDbContext.cs` will NOT add the table to an existing database. Create a migration and apply it manually (or drop the schema), or the table won't exist at runtime.
- All `DbSet`s are centralized in `Data/ApplicationDbContext.cs`.

## Auth (hand-rolled, not ASP.NET Identity)
- No `[Authorize]` attributes. `Login` stores `UserID`, `Role`, `FullName` in `HttpContext.Session`; every controller action checks the session directly and redirects to `Account/Login` (non-admin access to admin pages redirects to `Home/Index`). Mirror this pattern in new controllers.
- Passwords are BCrypt.Net-Next: hash/verify with `BCrypt.Net.BCrypt`.
- Flash messages use `TempData["Success"]` / `TempData["Error"]`.
- Self-registration creates accounts with `Role="Collector"`, `Status="Pending"`; admin approval happens in `AdminController` (`ApproveAccount`).

## Naming quirks — filename ≠ class name in several places
- `Models/Useraccount.cs` → class `UserAccount`
- `Controllers/Revenuetypecontoller.cs` → class `RevenueTypeController`
- `Controllers/Reportcontroller.cs` → class `ReportController`
- `Models/Editrequests.cs` → class `EditRequest`
- `Controllers/LongTermTenant.cs` → class `LongTermTenantController` (controller with in-file request DTOs, not a model)

## PDFs
QuestPDF requires `QuestPDF.Settings.License = LicenseType.Community` before generation; set it per-request (pattern already in `Reportcontroller.cs` and `Controllers/LongTermTenant.cs`).

## Repo hygiene
- There is NO `.gitignore`. `bin/`, `obj/`, `publish/`, and `.lscache` are tracked in git. Do not commit new build artifacts or touch files under `bin/`/`obj/`/`publish/`.