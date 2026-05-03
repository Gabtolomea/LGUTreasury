using LGUTreasury.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LGUTreasury.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // Prevent caching
            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";

            // Check if logged in
            var userID = HttpContext.Session.GetInt32("UserID");
            if (userID == null)
                return RedirectToAction("Login", "Account");

            // Pass user info to view
            ViewData["FullName"] = HttpContext.Session.GetString("FullName");
            ViewData["Role"] = HttpContext.Session.GetString("Role");
            ViewData["Initials"] = GetInitials(ViewData["FullName"]?.ToString());

            // Get user details
            var user = await _context.UserAccounts.FindAsync(userID);
            if (user != null)
            {
                ViewData["EmployeeID"] = user.EmployeeID;
                ViewData["MemberSince"] = user.CreatedAt.ToString("MMMM yyyy");
            }

            // Filter
            var filter = Request.Query["filter"].ToString();
            ViewData["CurrentFilter"] = string.IsNullOrEmpty(filter) ? "today" : filter;
            var today = DateTime.Today;

            IQueryable<LGUTreasury.Models.PaymentRecord> query = _context.PaymentRecords.Include(p => p.Payee);

            query = filter switch
            {
                "week"   => query.Where(p => p.DateIssued.Date >= today.AddDays(-7)),
                "month"  => query.Where(p => p.DateIssued.Month == today.Month && p.DateIssued.Year == today.Year),
                "year"   => query.Where(p => p.DateIssued.Year == today.Year),
                "2years" => query.Where(p => p.DateIssued >= today.AddYears(-2)),
                _        => query.Where(p => p.DateIssued.Date == today)
            };

            var payments = await query.OrderByDescending(p => p.DateIssued).ToListAsync();

            ViewBag.CollectedToday = payments.Sum(p => p.TotalAmount).ToString("N2");
            ViewBag.PaymentsToday = payments.Count;
            ViewBag.PendingRequests = 0;
            ViewBag.WeeklyTotal = payments.Sum(p => p.TotalAmount).ToString("N2");

            ViewBag.RecentRecords = payments.Take(5).Select(p => new
            {
                Initials  = $"{p.Payee?.Firstname?[0]}{p.Payee?.Lastname?[0]}".ToUpper(),
                PayeeName = $"{p.Payee?.Lastname}, {p.Payee?.Firstname}",
                Type      = "Payment",
                ReceiptNo = p.OfficialReceipt,
                Amount    = p.TotalAmount.ToString("N2"),
                Time      = p.DateIssued.ToString("hh:mm tt")
            }).ToList<dynamic>();

            ViewBag.RequestHistory = new List<dynamic>();

            return View();
        }

        private string GetInitials(string? fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return "?";
            var parts = fullName.Split(' ');
            if (parts.Length >= 2)
                return $"{parts[0][0]}{parts[^1][0]}".ToUpper();
            return fullName[0].ToString().ToUpper();
        }
    }
}