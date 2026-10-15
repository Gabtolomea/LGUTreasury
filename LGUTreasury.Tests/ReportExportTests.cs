using System.Text;
using ClosedXML.Excel;
using LGUTreasury.Data;
using Microsoft.AspNetCore.Mvc;

namespace LGUTreasury.Tests;

public class ReportExportTests
{
    private const string XlsxContentType =
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static ApplicationDbContext SeedOnePayment()
    {
        var db = TestFactory.NewDb();
        var type = TestData.AddType(db, 1, "Business Permit");
        TestData.AddPayment(db, 1, "OR-000001", DateTime.Today, type, firstName: "Juan", lastName: "Dela Cruz");
        return db;
    }

    [Fact]
    public async Task Generate_Pdf_ReturnsPdfFile()
    {
        using var db = SeedOnePayment();
        var controller = TestFactory.NewController(db);
        var today = DateTime.Today.ToString("yyyy-MM-dd");

        var result = await controller.Generate("daily", "pdf", today, today, null);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/pdf", file.ContentType);
        Assert.EndsWith(".pdf", file.FileDownloadName);
        Assert.Equal("%PDF", Encoding.ASCII.GetString(file.FileContents, 0, 4));
    }

    [Fact]
    public async Task Generate_Csv_ReturnsCsvFile()
    {
        using var db = SeedOnePayment();
        var controller = TestFactory.NewController(db);
        var today = DateTime.Today.ToString("yyyy-MM-dd");

        var result = await controller.Generate("daily", "csv", today, today, null);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("text/csv", file.ContentType);
        Assert.EndsWith(".csv", file.FileDownloadName);
        Assert.Contains("OR-000001", Encoding.UTF8.GetString(file.FileContents));
    }

    [Fact]
    public async Task Generate_Xlsx_ReturnsExcelFile_WithPaymentData()
    {
        using var db = SeedOnePayment();
        var controller = TestFactory.NewController(db);
        var today = DateTime.Today.ToString("yyyy-MM-dd");

        var result = await controller.Generate("custom", "xlsx", today, today, null);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal(XlsxContentType, file.ContentType);
        Assert.EndsWith(".xlsx", file.FileDownloadName);

        using var stream = new MemoryStream(file.FileContents);
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheet(1);

        var text = string.Join("\n", worksheet.CellsUsed().Select(c => c.GetString()));
        Assert.Contains("OR-000001", text);
        Assert.Contains("Juan", text);
    }

    [Fact]
    public async Task Generate_Xlsx_Header_ShowsReportTypeName()
    {
        using var db = SeedOnePayment();
        var controller = TestFactory.NewController(db);
        var today = DateTime.Today.ToString("yyyy-MM-dd");

        var result = await controller.Generate("custom", "xlsx", today, today, null);

        var file = Assert.IsType<FileContentResult>(result);
        using var stream = new MemoryStream(file.FileContents);
        using var workbook = new XLWorkbook(stream);

        Assert.Contains("Custom Report",
            string.Join("\n", workbook.Worksheet(1).CellsUsed().Select(c => c.GetString())));
    }

    [Theory]
    [InlineData("PDF")]
    [InlineData("Pdf")]
    [InlineData("CSV")]
    [InlineData("XLSX")]
    public async Task Generate_FormatIsCaseInsensitive(string format)
    {
        using var db = SeedOnePayment();
        var controller = TestFactory.NewController(db);
        var today = DateTime.Today.ToString("yyyy-MM-dd");

        var result = await controller.Generate("daily", format, today, today, null);

        var file = Assert.IsType<FileContentResult>(result);
        Assert.EndsWith($".{format.ToLowerInvariant()}", file.FileDownloadName);
    }

    [Fact]
    public async Task Generate_MissingFormat_ReturnsBadRequest()
    {
        using var db = SeedOnePayment();
        var controller = TestFactory.NewController(db);
        var today = DateTime.Today.ToString("yyyy-MM-dd");

        var result = await controller.Generate("daily", format: null!, today, today, null);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Generate_UnsupportedFormat_ReturnsBadRequest()
    {
        using var db = SeedOnePayment();
        var controller = TestFactory.NewController(db);
        var today = DateTime.Today.ToString("yyyy-MM-dd");

        var result = await controller.Generate("daily", "docx", today, today, null);

        Assert.IsType<BadRequestObjectResult>(result);
    }
}
