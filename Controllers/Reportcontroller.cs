using LGUTreasury.Data;
using LGUTreasury.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Text;

namespace LGUTreasury.Controllers
{
    public class ReportController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReportController(ApplicationDbContext context)
        {
            _context = context;
            QuestPDF.Settings.License = LicenseType.Community;
        }

        // GET: /Report/Index
        // Shows the Generate Report page with recent report history
        public async Task<IActionResult> Index()
        {
            var userID = HttpContext.Session.GetInt32("UserID");
            if (userID == null)
                return RedirectToAction("Login", "Account");

            ViewData["ActiveNav"] = "report";
            ViewData["FullName"] = HttpContext.Session.GetString("FullName");
            ViewData["Role"] = HttpContext.Session.GetString("Role");
            ViewData["Initials"] = GetInitials(ViewData["FullName"]?.ToString());

            // Load 10 most recent reports
            ViewBag.RecentReports = await _context.ReportLog
                .OrderByDescending(r => r.GeneratedAt)
                .Take(10)
                .ToListAsync();

            return View();
        }

        // GET: /Report/Generate
        // Called when officer clicks "Generate & Download"
        public async Task<IActionResult> Generate(string type, string format, string from, string to)
        {
            var userID = HttpContext.Session.GetInt32("UserID");
            if (userID == null)
                return RedirectToAction("Login", "Account");

            // Validate dates
            if (!DateTime.TryParse(from, out var fromDate) || !DateTime.TryParse(to, out var toDate))
                return BadRequest("Invalid date range.");

            if (fromDate > DateTime.Today || toDate > DateTime.Today)
                return BadRequest("Dates cannot be in the future.");

            if (fromDate > toDate)
                return BadRequest("From date cannot be after To date.");

            // Set toDate to end of day
            toDate = toDate.Date.AddDays(1).AddSeconds(-1);

            // Get all payments in the date range
            var payments = await _context.PaymentRecords
                .Include(p => p.Payee)
                .Include(p => p.CollectedBy)
                .Include(p => p.RecordLineItems)
                    .ThenInclude(l => l.RevenueType)
                .Where(p => p.DateIssued >= fromDate && p.DateIssued <= toDate)
                .OrderByDescending(p => p.DateIssued)
                .ToListAsync();

            // Get readable report type name
            var reportTypeName = type switch
            {
                "daily"   => "Daily Collection Report",
                "weekly"  => "Weekly Revenue Summary",
                "monthly" => "Monthly Revenue Summary",
                "annual"  => "Annual Revenue Summary",
                _         => "Revenue Summary"
            };

            var fileName = $"{reportTypeName.Replace(" ", "_")}_{fromDate:yyyy-MM-dd}_to_{toDate:yyyy-MM-dd}";
            var fullName = HttpContext.Session.GetString("FullName") ?? "Officer";

            // Save this report to the log table
            var log = new ReportLog
            {
                ReportType = reportTypeName,
                Format = format.ToUpper(),
                GeneratedAt = DateTime.Now,
                GeneratedByUserID = userID.Value
            };
            _context.ReportLog.Add(log);
            await _context.SaveChangesAsync();

            // Generate and return the file
            if (format == "csv")
                return GenerateCsv(payments, fileName, reportTypeName, fromDate, toDate);

            return GeneratePdf(payments, fileName, reportTypeName, fromDate, toDate, fullName);
        }
        private IActionResult GenerateCsv(
            List<PaymentRecord> payments, string fileName,
            string reportType, DateTime from, DateTime to)
        {
            var sb = new StringBuilder();
            sb.AppendLine("LGU TREASURER'S OFFICE COLLECTION RECORDING SYSTEM");
            sb.AppendLine($"Report Type: {reportType}");
            sb.AppendLine($"Period: {from:MMMM dd, yyyy} to {to:MMMM dd, yyyy}");
            sb.AppendLine($"Generated: {DateTime.Now:MMMM dd, yyyy hh:mm tt}");
            sb.AppendLine();
            sb.AppendLine("Transaction ID,OR Number,Payor,Collection Type,Payment Method,Base Amount,Surcharge,Interest,Total Amount,Date Issued");

            foreach (var p in payments)
            {
                var txnID  = "TXN-" + p.PaymentID.ToString("D6");
                var payee  = p.Payee != null ? $"{p.Payee.Lastname}, {p.Payee.Firstname}" : "—";
                var type   = p.RecordLineItems?.FirstOrDefault()?.RevenueType?.Name ?? "—";
                var method = p.PaymentMethod ?? "—";

                sb.AppendLine($"{txnID},{p.OfficialReceipt},\"{payee}\",\"{type}\",{method},{p.TotalBaseAmount:N2},{p.TotalSurcharge:N2},{p.TotalInterest:N2},{p.TotalAmount:N2},{p.DateIssued:yyyy-MM-dd hh:mm tt}");
            }

            sb.AppendLine();
            sb.AppendLine($"Total Records: {payments.Count}");
            sb.AppendLine($"Total Collected: {payments.Sum(p => p.TotalAmount):N2}");

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", fileName + ".csv");
        }

        // ── PDF GENERATOR ─────────────────────────────
        private IActionResult GeneratePdf(
            List<PaymentRecord> payments, string fileName,
            string reportType, DateTime from, DateTime to, string generatedBy)
        {
            var totalAmount = payments.Sum(p => p.TotalAmount);

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));

                    page.Header().Column(col =>
                    {
                        col.Item().Text("LGU TREASURER'S OFFICE COLLECTION RECORDING SYSTEM")
                            .Bold().FontSize(14).AlignCenter();
                        col.Item().Text(reportType)
                            .Bold().FontSize(11).AlignCenter();
                        col.Item().Text($"Period: {from:MMMM dd, yyyy} to {to:MMMM dd, yyyy}")
                            .FontSize(9).AlignCenter();
                        col.Item().PaddingTop(4).BorderBottom(1).BorderColor("#388E3C");
                    });

                    page.Content().PaddingTop(10).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(80);  // TXN ID
                            cols.ConstantColumn(70);  // OR Number
                            cols.RelativeColumn(2);   // Payor
                            cols.RelativeColumn(2);   // Type
                            cols.ConstantColumn(70);  // Method
                            cols.ConstantColumn(70);  // Base
                            cols.ConstantColumn(60);  // Surcharge
                            cols.ConstantColumn(60);  // Interest
                            cols.ConstantColumn(80);  // Total
                            cols.ConstantColumn(90);  // Date
                        });

                        static IContainer HeaderCell(IContainer c) =>
                            c.Background("#C8E6C9").Padding(5).AlignCenter();

                        table.Header(h =>
                        {
                            h.Cell().Element(HeaderCell).Text("Transaction ID").Bold().FontSize(8);
                            h.Cell().Element(HeaderCell).Text("OR Number").Bold().FontSize(8);
                            h.Cell().Element(HeaderCell).Text("Payor").Bold().FontSize(8);
                            h.Cell().Element(HeaderCell).Text("Collection Type").Bold().FontSize(8);
                            h.Cell().Element(HeaderCell).Text("Method").Bold().FontSize(8);
                            h.Cell().Element(HeaderCell).Text("Base Amount").Bold().FontSize(8);
                            h.Cell().Element(HeaderCell).Text("Surcharge").Bold().FontSize(8);
                            h.Cell().Element(HeaderCell).Text("Interest").Bold().FontSize(8);
                            h.Cell().Element(HeaderCell).Text("Total").Bold().FontSize(8);
                            h.Cell().Element(HeaderCell).Text("Date Issued").Bold().FontSize(8);
                        });

                        var rowIndex = 0;
                        foreach (var p in payments)
                        {
                            var bg = rowIndex % 2 == 0 ? "#ffffff" : "#f9f9f9";
                            rowIndex++;

                            var txnID  = "TXN-" + p.PaymentID.ToString("D6");
                            var payee  = p.Payee != null ? $"{p.Payee.Lastname}, {p.Payee.Firstname}" : "—";
                            var type   = p.RecordLineItems?.FirstOrDefault()?.RevenueType?.Name ?? "—";
                            var method = p.PaymentMethod ?? "—";

                            IContainer Cell(IContainer c) => c.Background(bg).Padding(4);

                            table.Cell().Element(Cell).Text(txnID).FontSize(8);
                            table.Cell().Element(Cell).Text(p.OfficialReceipt ?? "—").FontSize(8);
                            table.Cell().Element(Cell).Text(payee).FontSize(8);
                            table.Cell().Element(Cell).Text(type).FontSize(8);
                            table.Cell().Element(Cell).Text(method).FontSize(8);
                            table.Cell().Element(Cell).AlignRight().Text($"₱ {p.TotalBaseAmount:N2}").FontSize(8);
                            table.Cell().Element(Cell).AlignRight().Text($"₱ {p.TotalSurcharge:N2}").FontSize(8);
                            table.Cell().Element(Cell).AlignRight().Text($"₱ {p.TotalInterest:N2}").FontSize(8);
                            table.Cell().Element(Cell).AlignRight().Text($"₱ {p.TotalAmount:N2}").Bold().FontSize(8);
                            table.Cell().Element(Cell).Text(p.DateIssued.ToString("MM/dd/yyyy")).FontSize(8);
                        }

                        IContainer TotalCell(IContainer c) => c.Background("#e8f5e9").Padding(4);

                        table.Cell().ColumnSpan(5).Element(TotalCell)
                            .Text($"TOTAL — {payments.Count} record(s)").Bold().FontSize(8);
                        table.Cell().Element(TotalCell).AlignRight()
                            .Text($"₱ {payments.Sum(p => p.TotalBaseAmount):N2}").Bold().FontSize(8);
                        table.Cell().Element(TotalCell).AlignRight()
                            .Text($"₱ {payments.Sum(p => p.TotalSurcharge):N2}").Bold().FontSize(8);
                        table.Cell().Element(TotalCell).AlignRight()
                            .Text($"₱ {payments.Sum(p => p.TotalInterest):N2}").Bold().FontSize(8);
                        table.Cell().Element(TotalCell).AlignRight()
                            .Text($"₱ {totalAmount:N2}").Bold().FontSize(8);
                        table.Cell().Element(TotalCell).Text("").FontSize(8);
                    });

                    page.Footer().AlignRight().Text(text =>
                    {
                        text.Span($"Generated by: {generatedBy}  |  ").FontSize(8);
                        text.Span($"{DateTime.Now:MMMM dd, yyyy hh:mm tt}  |  Page ").FontSize(8);
                        text.CurrentPageNumber().FontSize(8);
                        text.Span(" of ").FontSize(8);
                        text.TotalPages().FontSize(8);
                    });
                });
            });

            var pdfBytes = document.GeneratePdf();
            return File(pdfBytes, "application/pdf", fileName + ".pdf");
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
