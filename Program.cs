using LGUTreasury;
using LGUTreasury.Data;
using Microsoft.EntityFrameworkCore;

// Load credentials/local settings from .env (git-ignored) before building configuration.
DotEnv.LoadDefault();

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddSession(); // add this

/*builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite(
        builder.Configuration.GetConnectionString("DefaultConnection")!
    ));
*/
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException(
        "Database connection string is not configured. Copy .env.example to .env and set " +
        "ConnectionStrings__DefaultConnection (or provide it as an environment variable).");

var applyMigrations = builder.Configuration.GetValue("Database:ApplyMigrations", true);
var disableHttpsRedirection = builder.Configuration.GetValue<bool>("DisableHttpsRedirection");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySQL(connectionString));


var app = builder.Build();

// ── Seed default admin ────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    if (applyMigrations)
    {
        const int databaseConnectionAttempts = 10;
        var databaseIsReachable = false;
        for (var attempt = 1; attempt <= databaseConnectionAttempts; attempt++)
        {
            if (context.Database.CanConnect())
            {
                databaseIsReachable = true;
                break;
            }

            if (attempt < databaseConnectionAttempts)
                Thread.Sleep(TimeSpan.FromSeconds(2));
        }

        if (!databaseIsReachable)
            throw new InvalidOperationException("The database could not be reached for migrations.");

        context.Database.Migrate();
    }
    else
    {
        context.Database.EnsureCreated();
    }

    DatabaseSchemaRepair.EnsureMissingColumns(context);

    if (!context.UserAccounts.Any(u => u.Role == "Admin"))
    {
        var initialAdminEmployeeId = builder.Configuration["INITIAL_ADMIN_EMPLOYEE_ID"];
        var initialAdminPassword = builder.Configuration["INITIAL_ADMIN_PASSWORD"];

        if (string.IsNullOrWhiteSpace(initialAdminEmployeeId) ||
            string.IsNullOrWhiteSpace(initialAdminPassword) ||
            initialAdminPassword.Length < 12)
        {
            throw new InvalidOperationException(
                "No administrator account exists. Set INITIAL_ADMIN_EMPLOYEE_ID and " +
                "INITIAL_ADMIN_PASSWORD (at least 12 characters) for the first startup.");
        }

        context.UserAccounts.Add(new LGUTreasury.Models.UserAccount
        {
            EmployeeID   = initialAdminEmployeeId.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(initialAdminPassword),
            Role         = "Admin",
            Status       = "Active",
            FirstName    = "System",
            LastName     = "Administrator",
            IsActive     = true,
            CreatedAt    = DateTime.Now
        });
        context.SaveChanges();
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

if (!disableHttpsRedirection)
{
    // Keep the internal container health endpoint reachable over HTTP. All other
    // application routes continue to be redirected to HTTPS.
    app.UseWhen(
        context => !context.Request.Path.StartsWithSegments("/health"),
        branch => branch.UseHttpsRedirection());
}
app.UseStaticFiles();
app.UseRouting();
app.UseSession(); 
app.UseAuthorization();

// Liveness only confirms that the ASP.NET Core process can answer requests.
app.MapGet("/health/live", () => Results.Ok(new { status = "alive" }));

// Readiness verifies the application's actual database connection. Do not expose
// exception details or connection-string information through this public endpoint.
Func<ApplicationDbContext, CancellationToken, Task<IResult>> readinessCheck = async
    (db, cancellationToken) =>
    {
        try
        {
            var databaseIsReachable = await db.Database.CanConnectAsync(cancellationToken);
            return databaseIsReachable
                ? Results.Ok(new { status = "ready", database = "reachable" })
                : Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
        catch
        {
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        }
    };

app.MapGet("/health/ready", readinessCheck);
// Keep the old path working for existing local checks and monitoring clients.
app.MapGet("/health", readinessCheck);

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}"
);
// Seed data
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    
    if (!context.RevenueCategories.Any())
    {
        context.RevenueCategories.AddRange(
            new LGUTreasury.Models.RevenueCategory { CategoryID = "TAX", Name = "Tax" },
            new LGUTreasury.Models.RevenueCategory { CategoryID = "FEE", Name = "Fee" },
            new LGUTreasury.Models.RevenueCategory { CategoryID = "PERMIT", Name = "Permit" },
            new LGUTreasury.Models.RevenueCategory { CategoryID = "RENTAL", Name = "Rental" },
            new LGUTreasury.Models.RevenueCategory { CategoryID = "WATER", Name = "Water" },
            new LGUTreasury.Models.RevenueCategory { CategoryID = "MARKET", Name = "Market" }
        );
        context.SaveChanges();
    }

    if (!context.RevenueTypes.Any())
    {
        context.RevenueTypes.AddRange(
            new LGUTreasury.Models.RevenueType { CategoryID = "PERMIT", Name = "Mayor's Permit", BaseRate = 800.00m, IsRecurring = false, IsActive = true },
            new LGUTreasury.Models.RevenueType { CategoryID = "TAX", Name = "Business Tax", BaseRate = 500.00m, IsRecurring = false, IsActive = true },
            new LGUTreasury.Models.RevenueType { CategoryID = "FEE", Name = "Real Property Tax", BaseRate = 1200.00m, IsRecurring = false, IsActive = true },
            new LGUTreasury.Models.RevenueType { CategoryID = "MARKET", Name = "Market Stall Fee", BaseRate = 200.00m, IsRecurring = false, IsActive = true },
            new LGUTreasury.Models.RevenueType { CategoryID = "WATER", Name = "Water Fees", BaseRate = 150.00m, IsRecurring = false, IsActive = true },
            new LGUTreasury.Models.RevenueType { CategoryID = "PERMIT", Name = "Building Permit", BaseRate = 300.00m, IsRecurring = false, IsActive = true },
            new LGUTreasury.Models.RevenueType { CategoryID = "PERMIT", Name = "Burial Permit", BaseRate = 100.00m, IsRecurring = false, IsActive = true },
            new LGUTreasury.Models.RevenueType { CategoryID = "FEE", Name = "CTC", BaseRate = 120.00m, IsRecurring = false, IsActive = true }
        );
        context.SaveChanges();
    }
    if (!context.RevenuePolicies.Any())
{
    var types = context.RevenueTypes.ToList();
    foreach (var type in types)
    {
        context.RevenuePolicies.Add(new LGUTreasury.Models.RevenuePolicy
        {
            TypeID = type.TypeID,
            SurchargeRate = 25.00m,
            InterestRate = 2.00m
        });
    }
    context.SaveChanges();
}
}//original code
app.Run();
