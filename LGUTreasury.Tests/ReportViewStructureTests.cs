namespace LGUTreasury.Tests;

public class ReportViewStructureTests
{
    [Fact]
    public void ReportView_DoesNotOfferWeeklyReport()
    {
        var view = RepoPaths.ReportView();

        Assert.DoesNotContain("Weekly Revenue Summary", view);
        Assert.DoesNotContain("value=\"weekly\"", view);
        Assert.DoesNotContain("type === 'weekly'", view);
    }

    [Fact]
    public void ReportView_OffersCustomReport()
    {
        var view = RepoPaths.ReportView();

        Assert.Contains("value=\"custom\"", view);
        Assert.Contains("Custom Report", view);
    }

    [Theory]
    [InlineData("daily", "Today's Report")]
    [InlineData("monthly", "Monthly Report")]
    [InlineData("annual", "Annual Report")]
    public void ReportView_KeepsExistingReportTypes(string value, string label)
    {
        var view = RepoPaths.ReportView();

        Assert.Contains($"value=\"{value}\"", view);
        Assert.Contains(label, view);
    }

    [Fact]
    public void ReportView_KeepsFromToDateAndRevenueTypeFields()
    {
        var view = RepoPaths.ReportView();

        Assert.Contains("id=\"report-from\"", view);
        Assert.Contains("id=\"report-to\"", view);
        Assert.Contains("id=\"report-revenue-type\"", view);
    }

    [Theory]
    [InlineData("remittance", "Remittance")]
    [InlineData("monthly-remittance", "Monthly Remittance")]
    public void ReportView_OffersRemittanceReportTypes(string value, string label)
    {
        var view = RepoPaths.ReportView();

        Assert.Contains($"value=\"{value}\"", view);
        Assert.Contains(label, view);
    }

    [Fact]
    public void ReportView_MonthlyRemittance_AutoFillsMonthStartToDate()
    {
        var view = RepoPaths.ReportView();

        Assert.Contains("type === 'monthly-remittance'", view);
    }

    [Fact]
    public void ReportView_CustomReport_DoesNotAutoFillDates()
    {
        var view = RepoPaths.ReportView();

        Assert.Contains("type === 'custom'", view);
    }

    [Fact]
    public void ReportController_NoLongerHandlesWeeklyType()
    {
        var source = RepoPaths.ReportControllerSource();

        Assert.DoesNotContain("\"weekly\"", source);
        Assert.DoesNotContain("Weekly Revenue Summary", source);
    }

    [Fact]
    public void ReportController_HandlesCustomType()
    {
        var source = RepoPaths.ReportControllerSource();

        Assert.Contains("\"custom\"", source);
        Assert.Contains("Custom Report", source);
    }
}
