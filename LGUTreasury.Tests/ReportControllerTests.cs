using System.Text;
using LGUTreasury.Controllers;
using LGUTreasury.Data;
using Microsoft.AspNetCore.Mvc;

namespace LGUTreasury.Tests;

public class ReportControllerTests
{
    private static string CsvOf(FileContentResult file) => Encoding.UTF8.GetString(file.FileContents);

    [Fact]
    public async Task Generate_RedirectsToLogin_WhenNotAuthenticated()
    {
        using var db = TestFactory.NewDb();
        var controller = TestFactory.NewController(db, userId: null);

        var result = await controller.Generate("daily", "csv", "2026-01-01", "2026-01-02", null);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Login", redirect.ActionName);
        Assert.Equal("Account", redirect.ControllerName);
    }

    [Fact]
    public async Task Generate_ReturnsBadRequest_WhenDateFormatInvalid()
    {
        using var db = TestFactory.NewDb();
        var controller = TestFactory.NewController(db);

        var result = await controller.Generate("daily", "csv", "not-a-date", "2026-01-02", null);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Generate_ReturnsBadRequest_WhenFromIsAfterTo()
    {
        using var db = TestFactory.NewDb();
        var controller = TestFactory.NewController(db);

        var result = await controller.Generate("daily", "csv", "2026-01-10", "2026-01-01", null);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Generate_ReturnsBadRequest_WhenRangeIsInTheFuture()
    {
        using var db = TestFactory.NewDb();
        var controller = TestFactory.NewController(db);
        var tomorrow = DateTime.Today.AddDays(1).ToString("yyyy-MM-dd");
        var dayAfter = DateTime.Today.AddDays(2).ToString("yyyy-MM-dd");

        var result = await controller.Generate("daily", "csv", tomorrow, dayAfter, null);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Generate_ReturnsCsv_WithExpectedContentTypeAndFileName()
    {
        using var db = TestFactory.NewDb();
        var type = TestData.AddType(db, 1, "Business Permit");
        TestData.AddPayment(db, 1, "OR-000001", DateTime.Today, type);
        var controller = TestFactory.NewController(db);
        var today = DateTime.Today.ToString("yyyy-MM-dd");

        var result = await controller.Generate("daily", "csv", today, today, null);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("text/csv", file.ContentType);
        Assert.Contains("Daily_Collection_Report", file.FileDownloadName);
        Assert.EndsWith(".csv", file.FileDownloadName);
    }

    [Fact]
    public async Task Generate_Csv_IncludesOnlyPaymentsWithinDateRange()
    {
        using var db = TestFactory.NewDb();
        var type = TestData.AddType(db, 1, "Business Permit");
        TestData.AddPayment(db, 1, "OR-IN-RANGE", DateTime.Today, type);
        TestData.AddPayment(db, 2, "OR-OUT-RANGE", DateTime.Today.AddDays(-10), type);
        var controller = TestFactory.NewController(db);
        var today = DateTime.Today.ToString("yyyy-MM-dd");

        var result = await controller.Generate("daily", "csv", today, today, null);
        var csv = CsvOf(Assert.IsType<FileContentResult>(result));

        Assert.Contains("OR-IN-RANGE", csv);
        Assert.DoesNotContain("OR-OUT-RANGE", csv);
        Assert.Contains("Total Records: 1", csv);
    }

    [Fact]
    public async Task Generate_Csv_FiltersByRevenueType()
    {
        using var db = TestFactory.NewDb();
        var typeA = TestData.AddType(db, 1, "Business Permit");
        var typeB = TestData.AddType(db, 2, "Real Property Tax");
        TestData.AddPayment(db, 1, "OR-TYPE-A", DateTime.Today, typeA);
        TestData.AddPayment(db, 2, "OR-TYPE-B", DateTime.Today, typeB);
        var controller = TestFactory.NewController(db);
        var today = DateTime.Today.ToString("yyyy-MM-dd");

        var result = await controller.Generate("daily", "csv", today, today, revenueTypeId: 1);
        var csv = CsvOf(Assert.IsType<FileContentResult>(result));

        Assert.Contains("OR-TYPE-A", csv);
        Assert.DoesNotContain("OR-TYPE-B", csv);
        Assert.Contains("Revenue Type: Business Permit", csv);
    }

    [Theory]
    [InlineData("daily", "Daily Collection Report")]
    [InlineData("monthly", "Monthly Revenue Summary")]
    [InlineData("annual", "Annual Revenue Summary")]
    [InlineData("custom", "Custom Report")]
    public async Task Generate_Csv_UsesExpectedReportTypeName(string type, string expectedName)
    {
        using var db = TestFactory.NewDb();
        var revenueType = TestData.AddType(db, 1, "Business Permit");
        TestData.AddPayment(db, 1, "OR-000001", DateTime.Today, revenueType);
        var controller = TestFactory.NewController(db);
        var today = DateTime.Today.ToString("yyyy-MM-dd");

        var result = await controller.Generate(type, "csv", today, today, null);
        var csv = CsvOf(Assert.IsType<FileContentResult>(result));

        Assert.Contains($"Report Type: {expectedName}", csv);
    }

    [Fact]
    public async Task Generate_Csv_RemovedWeeklyType_FallsBackToGenericSummary()
    {
        using var db = TestFactory.NewDb();
        var revenueType = TestData.AddType(db, 1, "Business Permit");
        TestData.AddPayment(db, 1, "OR-000001", DateTime.Today, revenueType);
        var controller = TestFactory.NewController(db);
        var today = DateTime.Today.ToString("yyyy-MM-dd");

        var result = await controller.Generate("weekly", "csv", today, today, null);
        var csv = CsvOf(Assert.IsType<FileContentResult>(result));

        Assert.Contains("Report Type: Revenue Summary", csv);
        Assert.DoesNotContain("Weekly Revenue Summary", csv);
    }

    [Fact]
    public async Task Generate_WritesReportLogEntry()
    {
        using var db = TestFactory.NewDb();
        var revenueType = TestData.AddType(db, 1, "Business Permit");
        TestData.AddPayment(db, 1, "OR-000001", DateTime.Today, revenueType);
        var controller = TestFactory.NewController(db, userId: 7);
        var today = DateTime.Today.ToString("yyyy-MM-dd");

        await controller.Generate("custom", "csv", today, today, null);

        var log = Assert.Single(db.ReportLog);
        Assert.Equal("Custom Report", log.ReportType);
        Assert.Equal("CSV", log.Format);
        Assert.Equal(7, log.GeneratedByUserID);
    }

    [Fact]
    public async Task Generate_MakesTheGeneratedFileDownloadable()
    {
        using var db = TestFactory.NewDb();
        var revenueType = TestData.AddType(db, 1, "Business Permit");
        TestData.AddPayment(db, 1, "OR-000001", DateTime.Today, revenueType);
        var controller = TestFactory.NewController(db);
        var today = DateTime.Today.ToString("yyyy-MM-dd");

        var result = await controller.Generate("annual", "csv", today, today, null);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.NotEmpty(file.FileContents);
    }
}
