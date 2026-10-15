using System.Text;
using LGUTreasury.Data;
using Microsoft.AspNetCore.Mvc;

namespace LGUTreasury.Tests;

public class CsvSafetyTests
{
    private static async Task<string> GenerateCsvAsync(ApplicationDbContext db)
    {
        var controller = TestFactory.NewController(db);
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        var result = await controller.Generate("daily", "csv", today, today, null);
        var file = Assert.IsType<FileContentResult>(result);
        return Encoding.UTF8.GetString(file.FileContents);
    }

    [Fact]
    public async Task PayeeStartingWithEquals_IsNeutralized()
    {
        using var db = TestFactory.NewDb();
        var type = TestData.AddType(db, 1, "Business Permit");
        TestData.AddPayment(db, 1, "OR-000001", DateTime.Today, type,
            firstName: "A", lastName: "=cmd|'/c calc'!A1");

        var csv = await GenerateCsvAsync(db);

        // No CSV field may begin directly with a formula trigger character.
        Assert.DoesNotContain("\"=cmd", csv);
        Assert.DoesNotContain(",=cmd", csv);
        // It should be neutralized with a leading apostrophe instead.
        Assert.Contains("'=cmd", csv);
    }

    [Fact]
    public async Task PayeeWithEmbeddedQuotes_IsEscaped()
    {
        using var db = TestFactory.NewDb();
        var type = TestData.AddType(db, 1, "Business Permit");
        TestData.AddPayment(db, 1, "OR-000001", DateTime.Today, type,
            firstName: "Juan", lastName: "Dela \"Cruz\"");

        var csv = await GenerateCsvAsync(db);

        // RFC 4180: embedded quotes are doubled.
        Assert.Contains("Dela \"\"Cruz\"\"", csv);
    }

    [Fact]
    public async Task PayeeWithComma_StaysInsideQuotedField()
    {
        using var db = TestFactory.NewDb();
        var type = TestData.AddType(db, 1, "Business Permit");
        TestData.AddPayment(db, 1, "OR-000001", DateTime.Today, type,
            firstName: "Juan", lastName: "Dela Cruz");

        var csv = await GenerateCsvAsync(db);

        Assert.Contains("\"Dela Cruz, Juan\"", csv);
    }
}
