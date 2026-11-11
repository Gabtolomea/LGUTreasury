using LGUTreasury.Controllers;
using LGUTreasury.Data;
using LGUTreasury.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LGUTreasury.Tests;

public class HomeAndSchemaTests
{
    private static HomeController NewHomeController(
        ApplicationDbContext db,
        int? userId = null,
        string? role = null,
        string? fullName = null,
        bool configureSession = true)
    {
        var http = new DefaultHttpContext();
        if (configureSession)
        {
            http.Session = new TestSession();
            if (userId.HasValue) http.Session.SetInt32("UserID", userId.Value);
            if (role is not null) http.Session.SetString("Role", role);
            if (fullName is not null) http.Session.SetString("FullName", fullName);
        }

        return new HomeController(db)
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };
    }

    [Fact]
    public async Task Index_ReturnsZeroedDashboard_WhenDatabaseHasOnlySeededAdmin()
    {
        using var db = TestFactory.NewDb();
        db.UserAccounts.Add(new UserAccount
        {
            UserID = 1,
            EmployeeID = "ADMIN001",
            PasswordHash = "hash",
            Role = "Admin",
            Status = "Active",
            FirstName = "System",
            LastName = "Administrator",
            IsActive = true
        });
        db.SaveChanges();

        var controller = NewHomeController(db, userId: 1, role: "Admin", fullName: "System Administrator");

        var result = await controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("0.00", view.ViewData["CollectedTotal"]);
        Assert.Equal("0.00", view.ViewData["MtdTotal"]);
        Assert.Equal("0.00", view.ViewData["TodayTotal"]);
        Assert.Equal("0.00", view.ViewData["AveragePayment"]);
        Assert.Equal(0, view.ViewData["PaymentsCount"]);
        Assert.Equal(0, view.ViewData["PendingRequests"]);
        Assert.Equal("ADMIN001", view.ViewData["EmployeeID"]);
    }

    [Fact]
    public void Error_ReturnsErrorViewModel_AndSkipsLayoutDbQueries_WhenAuthenticated()
    {
        using var db = TestFactory.NewDb();
        var controller = NewHomeController(db, userId: 1, role: "Admin", fullName: "System Administrator");
        controller.HttpContext.TraceIdentifier = "trace-123";

        var result = controller.Error();

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ErrorViewModel>(view.Model);
        Assert.True(model.ShowRequestId);
        Assert.Equal(true, view.ViewData["SkipLayoutDbQueries"]);
        Assert.Equal(true, view.ViewData["IsAuthenticated"]);
        Assert.Equal("Admin", view.ViewData["Role"]);
    }

    [Fact]
    public void Error_HandlesMissingSession_WithoutThrowing()
    {
        using var db = TestFactory.NewDb();
        var controller = NewHomeController(db, configureSession: false);
        controller.HttpContext.TraceIdentifier = "trace-no-session";

        var result = controller.Error();

        var view = Assert.IsType<ViewResult>(result);
        Assert.IsType<ErrorViewModel>(view.Model);
        Assert.Equal(true, view.ViewData["SkipLayoutDbQueries"]);
        Assert.Equal(false, view.ViewData["IsAuthenticated"]);
    }

    [Fact]
    public void EnsureMissingColumns_DoesNothingOnInMemoryContext()
    {
        using var db = TestFactory.NewDb();

        var exception = Record.Exception(() => DatabaseSchemaRepair.EnsureMissingColumns(db));

        Assert.Null(exception);
    }
}
