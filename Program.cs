using LGUTreasury.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddSession(); // add this

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySQL(
        builder.Configuration.GetConnectionString("DefaultConnection")!
    ));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession(); 
app.UseAuthorization();

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
}
//original code
app.Run();