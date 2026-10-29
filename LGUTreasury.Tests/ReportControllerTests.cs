using System.Globalization;
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
    public async Task Generate_DetailedCsv_IncludesOnlyPaymentsWithinDateRange()
    {
        using var db = TestFactory.NewDb();
        var type = TestData.AddType(db, 1, "Business Permit");
        TestData.AddPayment(db, 1, "OR-IN-RANGE", DateTime.Today, type);
        TestData.AddPayment(db, 2, "OR-OUT-RANGE", DateTime.Today.AddDays(-10), type);
        var controller = TestFactory.NewController(db);
        var today = DateTime.Today.ToString("yyyy-MM-dd");

        var result = await controller.Generate("detailed", "csv", today, today, null);
        var csv = CsvOf(Assert.IsType<FileContentResult>(result));

        Assert.Contains("OR-IN-RANGE", csv);
        Assert.DoesNotContain("OR-OUT-RANGE", csv);
        Assert.Contains("Total Records: 1", csv);
    }

    [Fact]
    public async Task Generate_SummaryCsv_TotalsOnlyPaymentsWithinDateRange()
    {
        using var db = TestFactory.NewDb();
        var type = TestData.AddType(db, 1, "Business Permit");
        TestData.AddPayment(db, 1, "OR-IN-RANGE", DateTime.Today, type, baseAmount: 100m);
        TestData.AddPayment(db, 2, "OR-OUT-RANGE", DateTime.Today.AddDays(-10), type, baseAmount: 500m);
        var controller = TestFactory.NewController(db);
        var today = DateTime.Today.ToString("yyyy-MM-dd");

        var result = await controller.Generate("daily", "csv", today, today, null);
        var csv = CsvOf(Assert.IsType<FileContentResult>(result));

        // The summary matrix carries the in-range amount only, and the
        // out-of-range payment must not inflate the total.
        Assert.Contains("Business Permit", csv);
        Assert.Contains("100.00", csv);
        Assert.DoesNotContain("500.00", csv);
    }

    [Fact]
    public async Task Generate_DetailedCsv_FiltersByRevenueType()
    {
        using var db = TestFactory.NewDb();
        var typeA = TestData.AddType(db, 1, "Business Permit");
        var typeB = TestData.AddType(db, 2, "Real Property Tax");
        TestData.AddPayment(db, 1, "OR-TYPE-A", DateTime.Today, typeA);
        TestData.AddPayment(db, 2, "OR-TYPE-B", DateTime.Today, typeB);
        var controller = TestFactory.NewController(db);
        var today = DateTime.Today.ToString("yyyy-MM-dd");

        var result = await controller.Generate("detailed", "csv", today, today, revenueTypeId: 1);
        var csv = CsvOf(Assert.IsType<FileContentResult>(result));

        Assert.Contains("OR-TYPE-A", csv);
        Assert.DoesNotContain("OR-TYPE-B", csv);
        Assert.Contains("Revenue Type: Business Permit", csv);
    }

    [Fact]
    public async Task Generate_SummaryCsv_FiltersByRevenueType()
    {
        using var db = TestFactory.NewDb();
        var typeA = TestData.AddType(db, 1, "Business Permit");
        var typeB = TestData.AddType(db, 2, "Real Property Tax");
        TestData.AddPayment(db, 1, "OR-TYPE-A", DateTime.Today, typeA, baseAmount: 100m);
        TestData.AddPayment(db, 2, "OR-TYPE-B", DateTime.Today, typeB, baseAmount: 500m);
        var controller = TestFactory.NewController(db);
        var today = DateTime.Today.ToString("yyyy-MM-dd");

        var result = await controller.Generate("daily", "csv", today, today, revenueTypeId: 1);
        var csv = CsvOf(Assert.IsType<FileContentResult>(result));

        // Only the filtered revenue type becomes a row of the matrix.
        Assert.Contains("Business Permit", csv);
        Assert.DoesNotContain("Real Property Tax", csv);
        Assert.Contains("100.00", csv);
        Assert.DoesNotContain("500.00", csv);
    }

    [Theory]
    [InlineData("daily", "Daily Collection Report")]
    [InlineData("monthly", "Monthly Revenue Summary")]
    [InlineData("annual", "Annual Revenue Summary")]
    [InlineData("custom", "Custom Report")]
    [InlineData("remittance", "Remittance Summary")]
    [InlineData("monthly-remittance", "Monthly Remittance")]
    [InlineData("detailed", "Detailed Collection List")]
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
    public async Task Generate_SummaryCsv_UsesBaseAmountOnly()
    {
        using var db = TestFactory.NewDb();
        var type = TestData.AddType(db, 1, "Business Permit");
        TestData.AddPayment(db, 1, "OR-000001", DateTime.Today, type,
            baseAmount: 100m, surcharge: 10m, interest: 5m);
        var controller = TestFactory.NewController(db);
        var today = DateTime.Today.ToString("yyyy-MM-dd");

        var result = await controller.Generate("daily", "csv", today, today, null);
        var csv = CsvOf(Assert.IsType<FileContentResult>(result));

        // The summary uses the revenue type's base amount only.
        Assert.Contains("100.00", csv);
        Assert.DoesNotContain("115.00", csv);
    }

    [Fact]
    public async Task Generate_SummaryUsesBaseAmount_WhileRemittanceUsesLineTotal()
    {
        using var db = TestFactory.NewDb();
        var type = TestData.AddType(db, 1, "Business Permit");
        TestData.AddPayment(db, 1, "OR-000001", DateTime.Today, type,
            baseAmount: 100m, surcharge: 10m, interest: 5m);
        var controller = TestFactory.NewController(db);
        var today = DateTime.Today.ToString("yyyy-MM-dd");

        var summary = await controller.Generate("daily", "csv", today, today, null);
        var remittance = await controller.Generate("remittance", "csv", today, today, null);

        var summaryCsv = CsvOf(Assert.IsType<FileContentResult>(summary));
        var remittanceCsv = CsvOf(Assert.IsType<FileContentResult>(remittance));

        Assert.Contains("100.00", summaryCsv);
        Assert.Contains("115.00", remittanceCsv);
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

    [Theory]
    [InlineData("remittance")]
    [InlineData("monthly-remittance")]
    public async Task Generate_RemittanceCsv_EscapesRevenueTypeNameInHeader(string type)
    {
        using var db = TestFactory.NewDb();
        var revenueType = TestData.AddType(db, 1, "Build, Permit");
        TestData.AddPayment(db, 1, "OR-000001", DateTime.Today, revenueType);
        var controller = TestFactory.NewController(db);
        var today = DateTime.Today.ToString("yyyy-MM-dd");

        var result = await controller.Generate(type, "csv", today, today, revenueTypeId: 1);
        var csv = CsvOf(Assert.IsType<FileContentResult>(result));

        // A comma in the name must not spill into extra columns.
        Assert.Contains("\"Build, Permit\"", csv);
        Assert.Contains("\"Revenue Type: Build, Permit\"", csv);
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
        Assert.Equal("custom", log.ReportKind);
        Assert.Equal(DateTime.Today, log.FromDate);
        Assert.Equal(DateTime.Today, log.ToDate);
        Assert.Equal("Juan Dela Cruz", log.GeneratedByName);
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

    [Fact]
    public async Task Download_RecreatesReportWithoutAddingAnotherLog()
    {
        using var db = TestFactory.NewDb();
        var revenueType = TestData.AddType(db, 1, "Business Permit");
        TestData.AddPayment(db, 1, "OR-000001", DateTime.Today, revenueType);
        var controller = TestFactory.NewController(db);
        var today = DateTime.Today.ToString("yyyy-MM-dd");

        await controller.Generate("custom", "csv", today, today, null);
        var log = Assert.Single(db.ReportLog);

        var result = await controller.Download(log.LogID);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.EndsWith(".csv", file.FileDownloadName);
        Assert.Single(db.ReportLog);
    }

    [Fact]
    public async Task Generate_RemittanceCsv_GroupsTypesUnderTheirCategory()
    {
        using var db = TestFactory.NewDb();
        var fee = TestData.AddType(db, 1, "CTC Fee", "FEE");
        var misc = TestData.AddType(db, 2, "Water Fees", "MISC");
        TestData.AddPayment(db, 1, "OR-000001", DateTime.Today, fee, baseAmount: 100m);
        TestData.AddPayment(db, 2, "OR-000002", DateTime.Today, misc, baseAmount: 250m);
        var controller = TestFactory.NewController(db);
        var today = DateTime.Today.ToString("yyyy-MM-dd");

        var result = await controller.Generate("remittance", "csv", today, today, null);
        var csv = CsvOf(Assert.IsType<FileContentResult>(result));

        var feeSectionIndex = csv.IndexOf("CATEGORY FEE:", StringComparison.Ordinal);
        var feeIndex = csv.IndexOf("CTC Fee", StringComparison.Ordinal);
        var miscSectionIndex = csv.IndexOf("CATEGORY MISC:", StringComparison.Ordinal);
        var miscIndex = csv.IndexOf("Water Fees", StringComparison.Ordinal);
        Assert.True(feeSectionIndex >= 0, "FEE category header should be present.");
        Assert.True(miscSectionIndex >= 0, "MISC category header should be present.");
        Assert.True(feeSectionIndex < feeIndex, "FEE type should come after its category header.");
        Assert.True(miscSectionIndex < miscIndex, "MISC type should come after its category header.");
        Assert.DoesNotContain("MISCELLANEOUS", csv);
        Assert.Contains("TOTAL,350.00", csv);
    }

    [Fact]
    public async Task Generate_MonthlyRemittanceCsv_BuildsDayColumnMatrix()
    {
        using var db = TestFactory.NewDb();
        var type = TestData.AddType(db, 1, "Business Permit");
        TestData.AddPayment(db, 1, "OR-000001", DateTime.Today, type, baseAmount: 150m);
        var controller = TestFactory.NewController(db);
        var today = DateTime.Today.ToString("yyyy-MM-dd");

        var result = await controller.Generate("monthly-remittance", "csv", today, today, null);
        var csv = CsvOf(Assert.IsType<FileContentResult>(result));

        Assert.Contains("GENERAL COLLECTIONS", csv);

        var month = DateTime.Today.ToString("MMM", CultureInfo.InvariantCulture).ToUpperInvariant();
        if (month == "SEP") month = "SEPT";
        var dayHeader =
            $"{month}. {DateTime.Today.ToString("dd", CultureInfo.InvariantCulture)}, " +
            DateTime.Today.ToString("yyyy", CultureInfo.InvariantCulture);
        Assert.Contains(dayHeader, csv);

        Assert.Contains("\"Business Permit\"", csv);
        Assert.Contains("Total,150.00", csv);
    }

    [Fact]
    public async Task Generate_MonthlyRemittance_ReturnsBadRequest_WhenRangeExceeds31Days()
    {
        using var db = TestFactory.NewDb();
        var controller = TestFactory.NewController(db);
        var from = DateTime.Today.AddDays(-40).ToString("yyyy-MM-dd");
        var to = DateTime.Today.ToString("yyyy-MM-dd");

        var result = await controller.Generate("monthly-remittance", "csv", from, to, null);

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
