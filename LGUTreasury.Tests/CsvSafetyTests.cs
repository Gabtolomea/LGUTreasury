using System.Text;
using LGUTreasury.Data;
using Microsoft.AspNetCore.Mvc;

namespace LGUTreasury.Tests;

public class CsvSafetyTests
{
    private static async Task<string> GenerateCsvAsync(ApplicationDbContext db, string reportType)
    {
        var controller = TestFactory.NewController(db);
        var today = DateTime.Today.ToString("yyyy-MM-dd");
        var result = await controller.Generate(reportType, "csv", today, today, null);
        var file = Assert.IsType<FileContentResult>(result);
        return Encoding.UTF8.GetString(file.FileContents);
    }

    // The payor name reaches the spreadsheet through the detailed collection
    // list. The summary reports are a revenue-type x date matrix, so they do
    // not carry the payor at all.
    [Fact]
    public async Task PayeeStartingWithEquals_IsNeutralized()
    {
        using var db = TestFactory.NewDb();
        var type = TestData.AddType(db, 1, "Business Permit");
        TestData.AddPayment(db, 1, "OR-000001", DateTime.Today, type,
            firstName: "A", lastName: "=cmd|'/c calc'!A1");

        var csv = await GenerateCsvAsync(db, "detailed");

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

        var csv = await GenerateCsvAsync(db, "detailed");

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

        var csv = await GenerateCsvAsync(db, "detailed");

        Assert.Contains("\"Dela Cruz, Juan\"", csv);
    }

    // Revenue type names are free text that an admin can edit, so they are a
    // formula-injection vector in every report that lists them: the summary
    // matrix as a row label and the detailed list as "Collection Type".
    [Theory]
    [InlineData("daily")]
    [InlineData("detailed")]
    public async Task RevenueTypeNameStartingWithEquals_IsNeutralized(string reportType)
    {
        using var db = TestFactory.NewDb();
        var type = TestData.AddType(db, 1, "=cmd|'/c calc'!A1");
        TestData.AddPayment(db, 1, "OR-000001", DateTime.Today, type);

        var csv = await GenerateCsvAsync(db, reportType);

        Assert.DoesNotContain("\"=cmd", csv);
        Assert.DoesNotContain(",=cmd", csv);
        Assert.Contains("'=cmd", csv);
    }

    [Theory]
    [InlineData("daily")]
    [InlineData("detailed")]
    public async Task RevenueTypeNameWithEmbeddedQuotes_IsEscaped(string reportType)
    {
        using var db = TestFactory.NewDb();
        var type = TestData.AddType(db, 1, "Build\"ing Permit");
        TestData.AddPayment(db, 1, "OR-000001", DateTime.Today, type);

        var csv = await GenerateCsvAsync(db, reportType);

        Assert.Contains("Build\"\"ing Permit", csv);
    }
}
