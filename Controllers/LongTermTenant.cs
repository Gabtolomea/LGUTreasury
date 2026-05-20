using LGUTreasury.Data;
using LGUTreasury.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace LGUTreasury.Controllers
{
    public class LongTermTenantController : Controller
    {
        private readonly ApplicationDbContext _context;

        public LongTermTenantController(ApplicationDbContext context)
        {
            _context = context;
            QuestPDF.Settings.License = LicenseType.Community;
        }

        // GET: /LongTermTenant/Index
        public async Task<IActionResult> Index()
        {
            var userID = HttpContext.Session.GetInt32("UserID");
            if (userID == null) return RedirectToAction("Login", "Account");

            var role = HttpContext.Session.GetString("Role");
            if (role != "Officer") return RedirectToAction("Index", "Home");

            ViewData["ActiveNav"] = "longterm";
            ViewData["FullName"]  = HttpContext.Session.GetString("FullName");
            ViewData["Role"]      = role;
            ViewData["Initials"]  = GetInitials(ViewData["FullName"]?.ToString());

            var tenants = await _context.LongTermPayees
                .Include(t => t.AccountBillingTypes)
                .Include(t => t.MonthlyBills)
                .Where(t => t.IsActive)
                .OrderBy(t => t.LastName)
                .ToListAsync();

            // This month widgets
            var currentMonth = DateTime.Now.ToString("yyyy-MM");
            var allBillsThisMonth = await _context.MonthlyBills
                .Where(b => b.BillingMonth == currentMonth)
                .ToListAsync();

            ViewBag.ExpectedThisMonth  = allBillsThisMonth.Sum(b => b.BilledAmount);
            ViewBag.CollectedThisMonth = allBillsThisMonth.Where(b => b.Status == "Paid").Sum(b => b.BilledAmount);
            ViewBag.CurrentMonth       = DateTime.Now.ToString("MMMM yyyy");

            // All-time widgets
            var allBills = await _context.MonthlyBills.ToListAsync();
            ViewBag.TotalExpected  = allBills.Sum(b => b.BilledAmount);
            ViewBag.TotalCollected = allBills.Where(b => b.Status == "Paid").Sum(b => b.BilledAmount);

            // Total unpaid balance per tenant
            ViewBag.TotalBalances = await _context.MonthlyBills
                .Where(b => b.Status == "Unpaid")
                .GroupBy(b => b.LongTermPayeeID)
                .Select(g => new { LongTermPayeeID = g.Key, Total = g.Sum(b => b.BilledAmount) })
                .ToDictionaryAsync(x => x.LongTermPayeeID, x => x.Total);

            // Autocomplete options
            ViewBag.BillingTypeOptions = await _context.BillingTypeOptions
                .OrderBy(o => o.Name)
                .ToListAsync();

            return View(tenants);
        }

        // POST: /LongTermTenant/Create
        [HttpPost]
        public async Task<IActionResult> Create(
            string FirstName, string? MiddleName, string LastName,
            string? Suffix, string? ContactNumber, string? Address,
            string StartMonth, int BillGenerationDay,
            [FromBody] CreateTenantRequest? req)
        {
            var userID = HttpContext.Session.GetInt32("UserID");
            if (userID == null) return Json(new { success = false, message = "Not logged in." });
            return Json(new { success = false, message = "Use JSON endpoint." });
        }

        // POST: /LongTermTenant/CreateTenant
        [HttpPost]
        public async Task<IActionResult> CreateTenant([FromBody] CreateTenantRequest req)
        {
            var userID = HttpContext.Session.GetInt32("UserID");
            if (userID == null) return Json(new { success = false, message = "Not logged in." });

            if (string.IsNullOrWhiteSpace(req.FirstName) || string.IsNullOrWhiteSpace(req.LastName))
                return Json(new { success = false, message = "First and last name are required." });

            if (string.IsNullOrWhiteSpace(req.StartMonth))
                return Json(new { success = false, message = "Start month is required." });

            if (req.BillingTypes == null || !req.BillingTypes.Any())
                return Json(new { success = false, message = "Please add at least one billing type." });

            var tenant = new LongTermPayee
            {
                FirstName         = req.FirstName,
                MiddleName        = req.MiddleName,
                LastName          = req.LastName,
                Suffix            = req.Suffix,
                ContactNumber     = req.ContactNumber,
                Address           = req.Address,
                StartMonth        = req.StartMonth,
                BillGenerationDay = req.BillGenerationDay > 0 ? req.BillGenerationDay : 20,
                IsActive          = true,
                CreatedAt         = DateTime.Now
            };
            _context.LongTermPayees.Add(tenant);
            await _context.SaveChangesAsync();

            foreach (var bt in req.BillingTypes)
            {
                _context.AccountBillingTypes.Add(new AccountBillingType
                {
                    LongTermPayeeID = tenant.LongTermPayeeID,
                    BillingTypeName = bt.Name,
                    MonthlyRate     = bt.Rate,
                    IsActive        = true,
                    CreatedAt       = DateTime.Now
                });

                var exists = await _context.BillingTypeOptions.AnyAsync(o => o.Name == bt.Name);
                if (!exists)
                    _context.BillingTypeOptions.Add(new BillingTypeOption { Name = bt.Name, CreatedAt = DateTime.Now });
            }
            await _context.SaveChangesAsync();

            await GenerateBillsForTenant(tenant.LongTermPayeeID);
            return Json(new { success = true });
        }

        // GET: /LongTermTenant/Ledger/{id}
        public async Task<IActionResult> Ledger(int id)
        {
            var userID = HttpContext.Session.GetInt32("UserID");
            if (userID == null) return RedirectToAction("Login", "Account");

            var role = HttpContext.Session.GetString("Role");
            if (role != "Officer") return RedirectToAction("Index", "Home");

            ViewData["ActiveNav"] = "longterm";
            ViewData["FullName"]  = HttpContext.Session.GetString("FullName");
            ViewData["Role"]      = role;
            ViewData["Initials"]  = GetInitials(ViewData["FullName"]?.ToString());

            var tenant = await _context.LongTermPayees
                .Include(t => t.AccountBillingTypes)
                .Include(t => t.MonthlyBills)
                .FirstOrDefaultAsync(t => t.LongTermPayeeID == id);

            if (tenant == null) return RedirectToAction("Index");
            return View(tenant);
        }

        // GET: /LongTermTenant/PrintBill/{billID}
        [HttpGet]
        public async Task<IActionResult> PrintBill(int id)
        {
            var userID = HttpContext.Session.GetInt32("UserID");
            if (userID == null) return RedirectToAction("Login", "Account");

            var bill = await _context.MonthlyBills
                .Include(b => b.LongTermPayee)
                .FirstOrDefaultAsync(b => b.MonthlyBillID == id);

            if (bill == null) return NotFound();

            var tenant    = bill.LongTermPayee!;
            var fullName  = $"{tenant.LastName}, {tenant.FirstName}";
            var accountNo = "LTA-" + tenant.LongTermPayeeID.ToString("D6");
            var billNo    = "BILL-" + bill.MonthlyBillID.ToString("D6");
            var monthDisplay = DateTime.TryParse(bill.BillingMonth + "-01", out var d)
                               ? d.ToString("MMMM yyyy") : bill.BillingMonth ?? "";

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A5);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Arial"));

                    page.Content().Column(col =>
                    {
                        // Header
                        col.Item().AlignCenter().Text("Municipality of Ginatilan")
                            .Bold().FontSize(14);
                        col.Item().AlignCenter().Text("LGU Revenue Record System — Billing Statement")
                            .FontSize(9).FontColor("#666666");
                        col.Item().PaddingTop(6).PaddingBottom(6).BorderBottom(1).BorderColor("#388E3C");

                        // Tenant Info
                        col.Item().PaddingTop(10).Table(t =>
                        {
                            t.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); });
                            t.Cell().Text("Account No:").Bold();
                            t.Cell().Text(accountNo);
                            t.Cell().Text("Tenant Name:").Bold();
                            t.Cell().Text(fullName);
                            t.Cell().Text("Address:").Bold();
                            t.Cell().Text(tenant.Address ?? "—");
                        });

                        col.Item().PaddingTop(12).PaddingBottom(6).BorderBottom(1).BorderColor("#cccccc");

                        // Bill Details
                        col.Item().PaddingTop(10).Table(t =>
                        {
                            t.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); });
                            t.Cell().Text("Bill ID:").Bold();
                            t.Cell().Text(billNo);
                            t.Cell().Text("Billing Month:").Bold();
                            t.Cell().Text(monthDisplay);
                            t.Cell().Text("Billing Type:").Bold();
                            t.Cell().Text(bill.BillingType ?? "—");
                            t.Cell().Text("Amount Due:").Bold();
                            t.Cell().Text($"₱ {bill.BilledAmount:N2}").Bold().FontColor("#388E3C");
                            t.Cell().Text("Status:").Bold();
                            t.Cell().Text(bill.Status ?? "Unpaid")
                                .FontColor(bill.Status == "Paid" ? "#2E7D32" : "#e07b00").Bold();

                            if (bill.Status == "Paid")
                            {
                                t.Cell().Text("OR Number:").Bold();
                                t.Cell().Text(bill.ORNumber ?? "—");
                                t.Cell().Text("Date Paid:").Bold();
                                t.Cell().Text(bill.PaidAt?.ToString("MMMM dd, yyyy") ?? "—");
                            }
                        });

                        col.Item().PaddingTop(20).PaddingBottom(6).BorderBottom(1).BorderColor("#cccccc");

                        // Footer
                        col.Item().PaddingTop(10).AlignCenter()
                            .Text($"Generated on {DateTime.Now:MMMM dd, yyyy hh:mm tt}")
                            .FontSize(8).FontColor("#888888");
                        col.Item().AlignCenter()
                            .Text("This is a computer-generated document.")
                            .FontSize(8).FontColor("#888888");
                    });
                });
            });

            var pdfBytes = document.GeneratePdf();
            return File(pdfBytes, "application/pdf", $"Bill_{billNo}_{monthDisplay.Replace(" ", "_")}.pdf");
        }

        // POST: /LongTermTenant/MarkPaid
        [HttpPost]
        public async Task<IActionResult> MarkPaid(int BillID, string ORNumber)
        {
            var userID = HttpContext.Session.GetInt32("UserID");
            if (userID == null) return Json(new { success = false, message = "Not logged in." });

            if (string.IsNullOrWhiteSpace(ORNumber))
                return Json(new { success = false, message = "OR Number is required." });

            var bill = await _context.MonthlyBills.FindAsync(BillID);
            if (bill == null) return Json(new { success = false, message = "Bill not found." });

            bill.Status   = "Paid";
            bill.ORNumber = ORNumber;
            bill.PaidAt   = DateTime.Now;
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        // POST: /LongTermTenant/MarkUnpaid
        [HttpPost]
        public async Task<IActionResult> MarkUnpaid(int BillID)
        {
            var userID = HttpContext.Session.GetInt32("UserID");
            if (userID == null) return Json(new { success = false, message = "Not logged in." });

            var bill = await _context.MonthlyBills.FindAsync(BillID);
            if (bill == null) return Json(new { success = false, message = "Bill not found." });

            bill.Status   = "Unpaid";
            bill.ORNumber = null;
            bill.PaidAt   = null;
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        // POST: /LongTermTenant/UpdateBillAmount
        [HttpPost]
        public async Task<IActionResult> UpdateBillAmount(int BillID, decimal BilledAmount)
        {
            var userID = HttpContext.Session.GetInt32("UserID");
            if (userID == null) return Json(new { success = false, message = "Not logged in." });

            var bill = await _context.MonthlyBills.FindAsync(BillID);
            if (bill == null) return Json(new { success = false, message = "Bill not found." });

            bill.BilledAmount = BilledAmount;
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        // POST: /LongTermTenant/GenerateBills
        [HttpPost]
        public async Task<IActionResult> GenerateBills(int tenantID)
        {
            var userID = HttpContext.Session.GetInt32("UserID");
            if (userID == null) return Json(new { success = false, message = "Not logged in." });

            await GenerateBillsForTenant(tenantID);
            return Json(new { success = true });
        }

        // GET: /LongTermTenant/GetBillingTypeOptions
        [HttpGet]
        public async Task<IActionResult> GetBillingTypeOptions(string? query)
        {
            var options = await _context.BillingTypeOptions
                .Where(o => string.IsNullOrWhiteSpace(query) || o.Name.Contains(query))
                .OrderBy(o => o.Name)
                .Select(o => o.Name)
                .ToListAsync();
            return Json(options);
        }

        // POST: /LongTermTenant/AddBill
        [HttpPost]
        public async Task<IActionResult> AddBill(
            int TenantID, string BillingMonth, int AccountBillingTypeID,
            string BillingType, decimal BilledAmount)
        {
            var userID = HttpContext.Session.GetInt32("UserID");
            if (userID == null) return Json(new { success = false, message = "Not logged in." });

            var exists = await _context.MonthlyBills.AnyAsync(b =>
                b.LongTermPayeeID      == TenantID &&
                b.AccountBillingTypeID == AccountBillingTypeID &&
                b.BillingMonth         == BillingMonth);

            if (exists)
                return Json(new { success = false, message = "A bill for this month and type already exists." });

            _context.MonthlyBills.Add(new MonthlyBill
            {
                LongTermPayeeID      = TenantID,
                AccountBillingTypeID = AccountBillingTypeID,
                BillingMonth         = BillingMonth,
                BillingType          = BillingType,
                BilledAmount         = BilledAmount,
                Status               = "Unpaid",
                CreatedAt            = DateTime.Now
            });
            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        // ── Private: Generate bills ──────────────────
        private async Task GenerateBillsForTenant(int tenantID)
        {
            var tenant = await _context.LongTermPayees
                .Include(t => t.AccountBillingTypes)
                .FirstOrDefaultAsync(t => t.LongTermPayeeID == tenantID);

            if (tenant == null) return;

            var activeBillingTypes = tenant.AccountBillingTypes?.Where(bt => bt.IsActive).ToList();
            if (activeBillingTypes == null || !activeBillingTypes.Any()) return;

            if (!DateTime.TryParse(tenant.StartMonth + "-01", out var startDate)) return;

            var currentMonth = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
            var cursor       = new DateTime(startDate.Year, startDate.Month, 1);

            while (cursor <= currentMonth)
            {
                var monthStr = cursor.ToString("yyyy-MM");
                foreach (var bt in activeBillingTypes)
                {
                    var exists = await _context.MonthlyBills.AnyAsync(b =>
                        b.LongTermPayeeID      == tenantID &&
                        b.AccountBillingTypeID == bt.AccountBillingTypeID &&
                        b.BillingMonth         == monthStr);

                    if (!exists)
                    {
                        _context.MonthlyBills.Add(new MonthlyBill
                        {
                            LongTermPayeeID      = tenantID,
                            AccountBillingTypeID = bt.AccountBillingTypeID,
                            BillingMonth         = monthStr,
                            BillingType          = bt.BillingTypeName,
                            BilledAmount         = bt.MonthlyRate,
                            Status               = "Unpaid",
                            CreatedAt            = DateTime.Now
                        });
                    }
                }
                cursor = cursor.AddMonths(1);
            }
            await _context.SaveChangesAsync();
        }

        private string GetInitials(string? fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return "?";
            var parts = fullName.Split(' ');
            if (parts.Length >= 2) return $"{parts[0][0]}{parts[^1][0]}".ToUpper();
            return fullName[0].ToString().ToUpper();
        }
    }

    public class CreateTenantRequest
    {
        public string FirstName      { get; set; } = "";
        public string? MiddleName    { get; set; }
        public string LastName       { get; set; } = "";
        public string? Suffix        { get; set; }
        public string? ContactNumber { get; set; }
        public string? Address       { get; set; }
        public string StartMonth     { get; set; } = "";
        public int BillGenerationDay { get; set; } = 20;
        public List<BillingTypeItem> BillingTypes { get; set; } = new();
    }

    public class BillingTypeItem
    {
        public string Name  { get; set; } = "";
        public decimal Rate { get; set; }
    }
}
