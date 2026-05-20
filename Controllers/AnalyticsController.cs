using LGUTreasury.Data;
using LGUTreasury.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LGUTreasury.Controllers
{
    public class AnalyticsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AnalyticsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Analytics/Index
        public IActionResult Index()
        {
            var userID = HttpContext.Session.GetInt32("UserID");
            if (userID == null) return RedirectToAction("Login", "Account");

            var role = HttpContext.Session.GetString("Role");
            ViewData["ActiveNav"] = "analytics";
            ViewData["FullName"]  = HttpContext.Session.GetString("FullName");
            ViewData["Role"]      = role;
            ViewData["Initials"]  = GetInitials(ViewData["FullName"]?.ToString());

            return View();
        }

        // GET: /Analytics/GetData?filter=thismonth&from=&to=
        [HttpGet]
        public async Task<IActionResult> GetData(string filter, string? from, string? to)
        {
            var (currentFrom, currentTo, compareFrom, compareTo, label, compareLabel) =
                GetDateRanges(filter, from, to);

            // ── ONE-TIME PAYMENTS ─────────────────────────
            // Load all payments with line items and revenue types
            var allPayments = await _context.PaymentRecords
                .Include(p => p.RecordLineItems).ThenInclude(l => l.RevenueType)
                .ToListAsync();

            var currentPayments = allPayments
                .Where(p => p.DateIssued.Date >= currentFrom && p.DateIssued.Date <= currentTo)
                .ToList();

            var comparePayments = allPayments
                .Where(p => p.DateIssued.Date >= compareFrom && p.DateIssued.Date <= compareTo)
                .ToList();

            // Group by revenue type using LineTotal (stays in sync with edits)
            var currentByType = currentPayments
                .SelectMany(p => p.RecordLineItems ?? new List<RecordLineItem>())
                .GroupBy(l => l.RevenueType?.Name ?? "Unknown")
                .ToDictionary(g => g.Key, g => g.Sum(l => l.LineTotal));

            var compareByType = comparePayments
                .SelectMany(p => p.RecordLineItems ?? new List<RecordLineItem>())
                .GroupBy(l => l.RevenueType?.Name ?? "Unknown")
                .ToDictionary(g => g.Key, g => g.Sum(l => l.LineTotal));

            var allTypeLabels = currentByType.Keys.Union(compareByType.Keys).OrderBy(x => x).ToList();

            // Totals using LineTotal
            var currentOneTimeTotal = currentPayments
                .SelectMany(p => p.RecordLineItems ?? new List<RecordLineItem>())
                .Sum(l => l.LineTotal);

            var compareOneTimeTotal = comparePayments
                .SelectMany(p => p.RecordLineItems ?? new List<RecordLineItem>())
                .Sum(l => l.LineTotal);

            // ── LONG TERM BILLS ───────────────────────────
            var allBills = await _context.MonthlyBills
                .Where(b => b.Status == "Paid")
                .ToListAsync();

            var currentBills = allBills
                .Where(b => b.PaidAt.HasValue &&
                       b.PaidAt.Value.Date >= currentFrom &&
                       b.PaidAt.Value.Date <= currentTo)
                .ToList();

            var compareBills = allBills
                .Where(b => b.PaidAt.HasValue &&
                       b.PaidAt.Value.Date >= compareFrom &&
                       b.PaidAt.Value.Date <= compareTo)
                .ToList();

            var currentBillsByType = currentBills
                .GroupBy(b => b.BillingType ?? "Unknown")
                .ToDictionary(g => g.Key, g => g.Sum(b => b.BilledAmount));

            var compareBillsByType = compareBills
                .GroupBy(b => b.BillingType ?? "Unknown")
                .ToDictionary(g => g.Key, g => g.Sum(b => b.BilledAmount));

            var allBillTypeLabels = currentBillsByType.Keys
                .Union(compareBillsByType.Keys).OrderBy(x => x).ToList();

            var currentLongTermTotal = currentBills.Sum(b => b.BilledAmount);
            var compareLongTermTotal = compareBills.Sum(b => b.BilledAmount);

            return Json(new
            {
                labels = new { current = label, compare = compareLabel },

                oneTime = new
                {
                    labels        = allTypeLabels,
                    current       = allTypeLabels.Select(l => currentByType.TryGetValue(l, out var v) ? v : 0).ToList(),
                    compare       = allTypeLabels.Select(l => compareByType.TryGetValue(l, out var v) ? v : 0).ToList(),
                    totalCurrent  = currentOneTimeTotal,
                    totalCompare  = compareOneTimeTotal,
                    pieLabels     = allTypeLabels,
                    pieCurrent    = allTypeLabels.Select(l => currentByType.TryGetValue(l, out var v) ? v : 0).ToList()
                },

                longTerm = new
                {
                    labels        = allBillTypeLabels,
                    current       = allBillTypeLabels.Select(l => currentBillsByType.TryGetValue(l, out var v) ? v : 0).ToList(),
                    compare       = allBillTypeLabels.Select(l => compareBillsByType.TryGetValue(l, out var v) ? v : 0).ToList(),
                    totalCurrent  = currentLongTermTotal,
                    totalCompare  = compareLongTermTotal,
                    pieLabels     = allBillTypeLabels,
                    pieCurrent    = allBillTypeLabels.Select(l => currentBillsByType.TryGetValue(l, out var v) ? v : 0).ToList()
                },

                combined = new
                {
                    labels       = new[] { "One-Time Payments", "Long Term Bills" },
                    current      = new[] { currentOneTimeTotal, currentLongTermTotal },
                    compare      = new[] { compareOneTimeTotal, compareLongTermTotal },
                    totalCurrent = currentOneTimeTotal + currentLongTermTotal,
                    totalCompare = compareOneTimeTotal + compareLongTermTotal
                }
            });
        }

        // ── Date Range Helper ─────────────────────────────
        private (DateTime, DateTime, DateTime, DateTime, string, string) GetDateRanges(
            string filter, string? from, string? to)
        {
            var today = DateTime.Today;

            return filter switch
            {
                "today" => (
                    today, today,
                    today.AddDays(-1), today.AddDays(-1),
                    "Today", "Yesterday"
                ),
                "yesterday" => (
                    today.AddDays(-1), today.AddDays(-1),
                    today.AddDays(-2), today.AddDays(-2),
                    "Yesterday", "2 Days Ago"
                ),
                "thisweek" => (
                    today.AddDays(-(int)today.DayOfWeek),
                    today,
                    today.AddDays(-(int)today.DayOfWeek - 7),
                    today.AddDays(-(int)today.DayOfWeek - 1),
                    "This Week", "Last Week"
                ),
                "thismonth" => (
                    new DateTime(today.Year, today.Month, 1),
                    today,
                    new DateTime(today.Year, today.Month, 1).AddMonths(-1),
                    new DateTime(today.Year, today.Month, 1).AddDays(-1),
                    "This Month", "Last Month"
                ),
                "thisyear" => (
                    new DateTime(today.Year, 1, 1),
                    today,
                    new DateTime(today.Year - 1, 1, 1),
                    new DateTime(today.Year - 1, 12, 31),
                    "This Year", "Last Year"
                ),
                "custom" when DateTime.TryParse(from, out var f) && DateTime.TryParse(to, out var t) => (
                    f, t,
                    f.AddYears(-1), t.AddYears(-1),
                    $"{f:MMM dd} – {t:MMM dd, yyyy}",
                    $"{f.AddYears(-1):MMM dd} – {t.AddYears(-1):MMM dd, yyyy}"
                ),
                _ => (
                    new DateTime(today.Year, today.Month, 1),
                    today,
                    new DateTime(today.Year, today.Month, 1).AddMonths(-1),
                    new DateTime(today.Year, today.Month, 1).AddDays(-1),
                    "This Month", "Last Month"
                )
            };
        }

        private string GetInitials(string? fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return "?";
            var parts = fullName.Split(' ');
            if (parts.Length >= 2) return $"{parts[0][0]}{parts[^1][0]}".ToUpper();
            return fullName[0].ToString().ToUpper();
        }
    }
}
