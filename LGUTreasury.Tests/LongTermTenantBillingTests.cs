using LGUTreasury.Controllers;
using LGUTreasury.Data;
using LGUTreasury.Models;
using LGUTreasury.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace LGUTreasury.Tests;

public class LongTermTenantBillingTests
{
    private static LongTermTenantController NewController(ApplicationDbContext db)
    {
        var http = new DefaultHttpContext { Session = new TestSession() };
        http.Session.SetInt32("UserID", 1);

        return new LongTermTenantController(db)
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };
    }

    private static int SeedTenant(
        ApplicationDbContext db,
        string startMonth,
        int billGenerationDay = 20,
        bool isActive = true,
        bool isBillingEnabled = true)
    {
        var tenant = new LongTermPayee
        {
            FirstName = "Test",
            LastName = "Tenant",
            StartMonth = startMonth,
            BillGenerationDay = billGenerationDay,
            IsActive = isActive,
            IsBillingEnabled = isBillingEnabled,
            CreatedAt = DateTime.Now
        };
        db.LongTermPayees.Add(tenant);
        db.SaveChanges();

        db.AccountBillingTypes.Add(new AccountBillingType
        {
            LongTermPayeeID = tenant.LongTermPayeeID,
            BillingTypeName = "Water Fees",
            MonthlyRate = 100m,
            IsActive = true,
            CreatedAt = DateTime.Now
        });
        db.SaveChanges();

        return tenant.LongTermPayeeID;
    }

    [Fact]
    public async Task GenerateBills_BeforeGenerationDay_CreatesNoBillForCurrentMonth()
    {
        var today = DateTime.Today;
        if (today.Day >= 20)
            return;

        using var db = TestFactory.NewDb();
        var tenantId = SeedTenant(db, today.ToString("yyyy-MM"));
        var controller = NewController(db);

        await controller.GenerateBills(tenantId);

        Assert.Empty(db.MonthlyBills.ToList());
    }

    [Fact]
    public async Task GenerateBills_OnGenerationDay_CreatesBillForCurrentMonth()
    {
        using var db = TestFactory.NewDb();
        var today = DateTime.Today;
        var tenantId = SeedTenant(db, today.ToString("yyyy-MM"), today.Day);
        var controller = NewController(db);

        await controller.GenerateBills(tenantId);

        var bill = Assert.Single(db.MonthlyBills.ToList());
        Assert.Equal(today.ToString("yyyy-MM"), bill.BillingMonth);
        Assert.Equal("Water Fees", bill.BillingType);
        Assert.Equal(100m, bill.BilledAmount);
        Assert.Equal("Unpaid", bill.Status);
    }

    [Fact]
    public async Task GenerateBills_WithPastStartMonth_BackfillsEveryElapsedMonth()
    {
        using var db = TestFactory.NewDb();
        var today = DateTime.Today;
        var start = new DateTime(today.Year, today.Month, 1).AddMonths(-2);
        var tenantId = SeedTenant(db, start.ToString("yyyy-MM"), 1);
        var controller = NewController(db);

        await controller.GenerateBills(tenantId);

        var months = db.MonthlyBills.Select(b => b.BillingMonth).OrderBy(m => m).ToList();
        Assert.Equal(new[]
        {
            start.ToString("yyyy-MM"),
            start.AddMonths(1).ToString("yyyy-MM"),
            start.AddMonths(2).ToString("yyyy-MM")
        }, months);
    }

    [Fact]
    public async Task GenerateBills_CalledTwice_DoesNotDuplicateBills()
    {
        using var db = TestFactory.NewDb();
        var today = DateTime.Today;
        var tenantId = SeedTenant(db, today.ToString("yyyy-MM"), 1);
        var controller = NewController(db);

        await controller.GenerateBills(tenantId);
        await controller.GenerateBills(tenantId);

        Assert.Single(db.MonthlyBills.ToList());
    }

    [Fact]
    public async Task GenerateBills_WhenBillingDisabled_CreatesNoBills()
    {
        using var db = TestFactory.NewDb();
        var today = DateTime.Today;
        var tenantId = SeedTenant(
            db, today.ToString("yyyy-MM"), 1,
            isActive: true, isBillingEnabled: false);
        var controller = NewController(db);

        await controller.GenerateBills(tenantId);

        Assert.Empty(db.MonthlyBills.ToList());
    }

    [Fact]
    public void Program_RegistersMonthlyBillingBackgroundService()
    {
        Assert.Contains(
            "AddHostedService<MonthlyBillingBackgroundService>",
            RepoPaths.Read("Program.cs"));
    }

    [Fact]
    public async Task MonthlyBillingBackgroundService_RunOnce_GeneratesBillsForEligibleTenants()
    {
        using var db = TestFactory.NewDb();
        var today = DateTime.Today;
        var eligibleId = SeedTenant(db, today.ToString("yyyy-MM"), 1);
        var disabledId = SeedTenant(
            db, today.ToString("yyyy-MM"), 1,
            isActive: true, isBillingEnabled: false);

        var service = new MonthlyBillingBackgroundService(
            new TestScopeFactory(db),
            NullLogger<MonthlyBillingBackgroundService>.Instance);

        await service.RunOnceAsync();

        var bill = Assert.Single(db.MonthlyBills.ToList());
        Assert.Equal(eligibleId, bill.LongTermPayeeID);
        Assert.DoesNotContain(db.MonthlyBills, b => b.LongTermPayeeID == disabledId);
    }

    private sealed class TestScopeFactory : IServiceScopeFactory
    {
        private readonly ApplicationDbContext _db;

        public TestScopeFactory(ApplicationDbContext db) => _db = db;

        public IServiceScope CreateScope() => new TestScope(_db);

        private sealed class TestScope : IServiceScope
        {
            private readonly ServiceProvider _provider;

            public TestScope(ApplicationDbContext db)
            {
                var services = new ServiceCollection();
                services.AddSingleton(db);
                _provider = services.BuildServiceProvider();
                ServiceProvider = _provider;
            }

            public IServiceProvider ServiceProvider { get; }

            public void Dispose() => _provider.Dispose();
        }
    }
}
