using System.Runtime.CompilerServices;
using LGUTreasury.Controllers;
using LGUTreasury.Data;
using LGUTreasury.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LGUTreasury.Tests;

internal sealed class TestSession : ISession
{
    private readonly Dictionary<string, byte[]> _store = new();

    public bool IsAvailable => true;
    public string Id { get; } = Guid.NewGuid().ToString();
    public IEnumerable<string> Keys => _store.Keys;

    public void Clear() => _store.Clear();
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task LoadAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    public void Remove(string key) => _store.Remove(key);
    public void Set(string key, byte[] value) => _store[key] = value;
    public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value!);
}

internal static class TestFactory
{
    public static ApplicationDbContext NewDb()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    public static ReportController NewController(
        ApplicationDbContext db, int? userId = 1, string? fullName = "Juan Dela Cruz")
    {
        var http = new DefaultHttpContext { Session = new TestSession() };
        if (userId.HasValue) http.Session.SetInt32("UserID", userId.Value);
        if (fullName is not null) http.Session.SetString("FullName", fullName);

        return new ReportController(db)
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };
    }
}

internal static class TestData
{
    public static RevenueType AddType(ApplicationDbContext db, int id, string name, string categoryId = "CAT1")
    {
        var categoryExists =
            db.RevenueCategories.Local.Any(c => c.CategoryID == categoryId) ||
            db.RevenueCategories.Any(c => c.CategoryID == categoryId);

        if (!categoryExists)
            db.RevenueCategories.Add(new RevenueCategory { CategoryID = categoryId, Name = "Category " + categoryId });

        var type = new RevenueType
        {
            TypeID = id,
            CategoryID = categoryId,
            Name = name,
            BaseRate = 10m,
            IsActive = true
        };
        db.RevenueTypes.Add(type);
        db.SaveChanges();
        return type;
    }

    public static void AddUser(ApplicationDbContext db, int id = 1)
    {
        if (db.UserAccounts.Any(u => u.UserID == id))
            return;

        db.UserAccounts.Add(new UserAccount
        {
            UserID = id,
            EmployeeID = "EMP-" + id,
            PasswordHash = "hash",
            Role = "Officer",
            FirstName = "Collector",
            LastName = id.ToString()
        });
    }

    public static PaymentRecord AddPayment(
        ApplicationDbContext db,
        int id,
        string officialReceipt,
        DateTime dateIssued,
        RevenueType type,
        string firstName = "Juan",
        string lastName = "Dela Cruz",
        decimal baseAmount = 100m)
    {
        AddUser(db, 1);

        var payment = new PaymentRecord
        {
            PaymentID = id,
            OfficialReceipt = officialReceipt,
            PayeeID = id,
            Payee = new Payee { PayeeID = id, Firstname = firstName, Lastname = lastName },
            DateIssued = dateIssued,
            CollectedBy_UserID = 1,
            PaymentMethod = "Cash",
            TotalBaseAmount = baseAmount,
            TotalSurcharge = 0m,
            TotalInterest = 0m,
            TotalAmount = baseAmount
        };

        payment.RecordLineItems.Add(new RecordLineItem
        {
            LineItemID = id,
            PaymentID = id,
            TypeID = type.TypeID,
            RevenueType = type,
            Quantity = 1,
            BaseAmount = baseAmount,
            LineTotal = baseAmount
        });

        db.PaymentRecords.Add(payment);
        db.SaveChanges();
        return payment;
    }
}

internal static class RepoPaths
{
    public static string Root([CallerFilePath] string here = "")
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(here)!);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "LGUTreasury.sln")))
            dir = dir.Parent;

        if (dir is null)
            throw new InvalidOperationException("Could not locate the repository root (LGUTreasury.sln).");

        return dir.FullName;
    }

    public static string Read(params string[] relativeParts) =>
        File.ReadAllText(Path.Combine(new[] { Root() }.Concat(relativeParts).ToArray()));

    public static string ReportView() => Read("Views", "Report", "Index.cshtml");
    public static string ReportControllerSource() => Read("Controllers", "Reportcontroller.cs");
}
