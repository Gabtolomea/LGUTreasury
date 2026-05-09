using LGUTreasury.Data;
using LGUTreasury.Models;
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
            
            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";

            
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

            // Filter — only week and month allowed now
            var filter = Request.Query["filter"].ToString();
            if (filter != "week" && filter != "month")
                filter = "week"; // default to week

            ViewData["CurrentFilter"] = filter;
            var today = DateTime.Today;

            IQueryable<PaymentRecord> query = _context.PaymentRecords
                .Include(p => p.Payee);

            query = filter == "month"
                ? query.Where(p => p.DateIssued.Month == today.Month && p.DateIssued.Year == today.Year)
                : query.Where(p => p.DateIssued.Date >= today.AddDays(-6));

            var payments = await query.OrderByDescending(p => p.DateIssued).ToListAsync();

            // Load line items separately
            var paymentIDs = payments.Select(p => p.PaymentID).ToList();
            var lineItems = await _context.RecordLineItems
                .Include(l => l.RevenueType)
                .Where(l => paymentIDs.Contains(l.PaymentID))
                .ToListAsync();

            ViewBag.CollectedTotal = payments.Sum(p => p.TotalAmount).ToString("N2");
            ViewBag.PaymentsCount  = payments.Count;

            // Real pending requests count from EditRequests table
            ViewBag.PendingRequests = await _context.Editrequests
                .Where(r => r.Status == "Pending")
                .CountAsync();

            // Recent records
            ViewBag.RecentRecords = payments.Take(5).Select(p => new RecentRecordViewModel
            {
                Initials  = $"{p.Payee?.Firstname?[0]}{p.Payee?.Lastname?[0]}".ToUpper(),
                PayeeName = $"{p.Payee?.Lastname}, {p.Payee?.Firstname}",
                Type      = lineItems.FirstOrDefault(l => l.PaymentID == p.PaymentID)?.RevenueType?.Name ?? "Payment",
                ReceiptNo = p.OfficialReceipt,
                Amount    = p.TotalAmount.ToString("N2"),
                Time = p.DateIssued.ToString("MMM dd") + " · " + p.CreatedAt.ToString("hh:mm tt")
            }).ToList();

            // Real recent requests for the logged-in user
            ViewBag.RecentRequests = await _context.Editrequests
                .Include(r => r.PaymentRecord)
                .Where(r => r.RequestedBy_UserID == userID)
                .OrderByDescending(r => r.CreatedAt)
                .Take(4)
                .ToListAsync();

                var monthStart = new DateTime(today.Year, today.Month, 1);
                var mtdTotal = await _context.PaymentRecords
                .Where(p => p.DateIssued >= monthStart && p.DateIssued <= today)
                .SumAsync(p => p.TotalAmount);
                ViewBag.MtdTotal = mtdTotal.ToString("N2");
                ViewBag.DaysThisMonth = today.Day;

                var topRevenue = await _context.RecordLineItems
               .Include(l => l.RevenueType)
               .Where(l => l.PaymentRecord != null && l.PaymentRecord.DateIssued.Date == today)
               .GroupBy(l => l.RevenueType != null ? l.RevenueType.Name : "Unknown")
               .Select(g => new {
               Name = g.Key,
               Total = g.Sum(l => l.LineTotal),
               Count = g.Count()
             })
               .OrderByDescending(g => g.Total)
               .FirstOrDefaultAsync();
               ViewBag.TopRevenueName = topRevenue?.Name ?? "No data";
               ViewBag.TopRevenueTotal = topRevenue?.Total.ToString("N2") ?? "0.00";
               ViewBag.TopRevenueCount = topRevenue?.Count ?? 0;

               // Bar chart data — daily totals for the week
              var weekStart = today.AddDays(-6);
              var allPayments = await _context.PaymentRecords
              .Where(p => p.DateIssued.Date >= weekStart && p.DateIssued.Date <= today)
              .ToListAsync();

             var dailyTotals = Enumerable.Range(0, 7)
             .Select(i => {
             var day = weekStart.AddDays(i);
              return allPayments
             .Where(p => p.DateIssued.Date == day)
             .Sum(p => p.TotalAmount);
            }).ToList();

            var maxTotal = dailyTotals.Max() == 0 ? 1 : dailyTotals.Max();
            ViewBag.BarHeights = dailyTotals
            .Select(t => (int)Math.Round((t / maxTotal) * 100))
            .ToList();
            ViewBag.BarLabels = Enumerable.Range(0, 7)
            .Select(i => weekStart.AddDays(i).ToString("ddd"))
            .ToList();

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
