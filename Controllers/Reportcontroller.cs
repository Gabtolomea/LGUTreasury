using LGUTreasury.Data;
using LGUTreasury.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;
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

            // Revenue types for the filter dropdown (includes inactive ones,
            // so older records can still be filtered; new types appear automatically)
            ViewBag.RevenueTypes = await _context.RevenueTypes
                .OrderBy(r => r.Name)
                .Select(r => new { r.TypeID, r.Name, r.IsActive })
                .ToListAsync();

            return View();
        }

        // GET: /Report/Generate
        // Called when officer clicks "Generate & Download"
        public async Task<IActionResult> Generate(string type, string format, string from, string to, int? revenueTypeId)
        {
            var userID = HttpContext.Session.GetInt32("UserID");
            if (userID == null)
                return RedirectToAction("Login", "Account");

            // Validate / normalize the requested file format (avoid NullReferenceException
            // and reject anything we cannot actually produce).
            var normalizedFormat = (format ?? string.Empty).Trim().ToLowerInvariant();
            if (normalizedFormat is not ("pdf" or "csv" or "xlsx"))
                return BadRequest("Unsupported file format. Use 'pdf', 'csv', or 'xlsx'.");

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
            var query = _context.PaymentRecords
                .Include(p => p.Payee)
                .Include(p => p.CollectedBy)
                .Include(p => p.RecordLineItems)
                    .ThenInclude(l => l.RevenueType)
                .Where(p => p.DateIssued >= fromDate && p.DateIssued <= toDate);

            // Optional: only payments for the selected revenue type
            if (revenueTypeId.HasValue)
                query = query.Where(p => p.RecordLineItems.Any(l => l.TypeID == revenueTypeId.Value));

            var payments = await query
                .OrderByDescending(p => p.DateIssued)
                .ToListAsync();

            // Get readable report type name
            var reportTypeName = type switch
            {
                "daily"   => "Daily Collection Report",
                "monthly" => "Monthly Revenue Summary",
                "annual"  => "Annual Revenue Summary",
                "custom"  => "Custom Report",
                _         => "Revenue Summary"
            };

            // Name of the selected revenue type (for the file name and report header)
            string? revenueTypeName = null;
            if (revenueTypeId.HasValue)
            {
                revenueTypeName = await _context.RevenueTypes
                    .Where(r => r.TypeID == revenueTypeId.Value)
                    .Select(r => r.Name)
                    .FirstOrDefaultAsync();
            }

            var fileNameSuffix = "";
            if (!string.IsNullOrEmpty(revenueTypeName))
            {
                var safe = new string(revenueTypeName
                    .Where(c => char.IsLetterOrDigit(c) || c == ' ' || c == '-')
                    .ToArray()).Trim().Replace(" ", "_");
                if (safe.Length > 0)
                    fileNameSuffix = "_" + safe;
            }

            var fileName = $"{reportTypeName.Replace(" ", "_")}{fileNameSuffix}_{fromDate:yyyy-MM-dd}_to_{toDate:yyyy-MM-dd}";
            var fullName = HttpContext.Session.GetString("FullName") ?? "Officer";

            // Save this report to the log table
            var log = new ReportLog
            {
                ReportType = reportTypeName,
                Format = normalizedFormat.ToUpper(),
                GeneratedAt = DateTime.Now,
                GeneratedByUserID = userID.Value
            };
            _context.ReportLog.Add(log);
            await _context.SaveChangesAsync();

            // Generate and return the file
            return normalizedFormat switch
            {
                "csv"  => GenerateCsv(payments, fileName, reportTypeName, fromDate, toDate, revenueTypeName),
                "xlsx" => GenerateXlsx(payments, fileName, reportTypeName, fromDate, toDate, revenueTypeName),
                _      => GeneratePdf(payments, fileName, reportTypeName, fromDate, toDate, fullName, revenueTypeName)
            };
        }

        private IActionResult GenerateCsv(
            List<PaymentRecord> payments, string fileName,
            string reportType, DateTime from, DateTime to, string? revenueTypeName = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine("LGU TREASURER'S OFFICE COLLECTION RECORDING SYSTEM");
            sb.AppendLine($"Report Type: {reportType}");
            if (!string.IsNullOrEmpty(revenueTypeName))
                sb.AppendLine($"Revenue Type: {revenueTypeName}");
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

                sb.AppendLine(string.Join(",",
                    CsvField(txnID),
                    CsvField(p.OfficialReceipt),
                    CsvField(payee),
                    CsvField(type),
                    CsvField(method),
                    Money(p.TotalBaseAmount),
                    Money(p.TotalSurcharge),
                    Money(p.TotalInterest),
                    Money(p.TotalAmount),
                    CsvField(p.DateIssued.ToString("yyyy-MM-dd hh:mm tt", CultureInfo.InvariantCulture))));
            }

            sb.AppendLine();
            sb.AppendLine($"Total Records: {payments.Count}");
            sb.AppendLine($"Total Collected: {Money(payments.Sum(p => p.TotalAmount))}");

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", fileName + ".csv");
        }

        // Escapes a value for CSV (RFC 4180) and neutralizes spreadsheet formula
        // injection: a leading =, +, -, @, tab or CR would otherwise be evaluated as
        // a formula by Excel / LibreOffice when the file is opened.
        private static string CsvField(string? value)
        {
            var v = value ?? string.Empty;

            if (v.Length > 0 && (v[0] == '=' || v[0] == '+' || v[0] == '-' ||
                                 v[0] == '@' || v[0] == '\t' || v[0] == '\r'))
            {
                v = "'" + v;
            }

            return "\"" + v.Replace("\"", "\"\"") + "\"";
        }

        private static string Money(decimal value) =>
            value.ToString("N2", CultureInfo.InvariantCulture);

        // ── EXCEL (XLSX) GENERATOR ────────────────────
        private IActionResult GenerateXlsx(
            List<PaymentRecord> payments, string fileName,
            string reportType, DateTime from, DateTime to, string? revenueTypeName = null)
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Revenue Summary");
            ws.Style.Font.FontName = "Arial";
            ws.Style.Font.FontSize = 10;

            ws.Cell(1, 1).Value = "LGU TREASURER'S OFFICE COLLECTION RECORDING SYSTEM";
            ws.Range(1, 1, 1, 10).Merge();
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 14;
            ws.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(2, 1).Value = reportType;
            ws.Range(2, 1, 2, 10).Merge();
            ws.Cell(2, 1).Style.Font.Bold = true;
            ws.Cell(2, 1).Style.Font.FontSize = 12;
            ws.Cell(2, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(3, 1).Value = $"Period: {from:MMMM dd, yyyy} to {to:MMMM dd, yyyy}";
            ws.Range(3, 1, 3, 10).Merge();
            ws.Cell(3, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            var headerRow = 4;
            if (!string.IsNullOrEmpty(revenueTypeName))
            {
                ws.Cell(4, 1).Value = $"Revenue Type: {revenueTypeName}";
                ws.Range(4, 1, 4, 10).Merge();
                ws.Cell(4, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                headerRow = 5;
            }

            string[] headers =
            {
                "Transaction ID", "OR Number", "Payor", "Collection Type", "Method",
                "Base Amount", "Surcharge", "Interest", "Total", "Date Issued"
            };
            for (var c = 0; c < headers.Length; c++)
            {
                var cell = ws.Cell(headerRow, c + 1);
                cell.Value = headers[c];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#C8E6C9");
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            var row = headerRow + 1;
            foreach (var p in payments)
            {
                var payee  = p.Payee != null ? $"{p.Payee.Lastname}, {p.Payee.Firstname}" : "—";
                var type   = p.RecordLineItems?.FirstOrDefault()?.RevenueType?.Name ?? "—";
                var method = p.PaymentMethod ?? "—";

                ws.Cell(row, 1).Value = "TXN-" + p.PaymentID.ToString("D6");
                ws.Cell(row, 2).Value = p.OfficialReceipt ?? "—";
                ws.Cell(row, 3).Value = payee;
                ws.Cell(row, 4).Value = type;
                ws.Cell(row, 5).Value = method;
                ws.Cell(row, 6).Value = (double)p.TotalBaseAmount;
                ws.Cell(row, 7).Value = (double)p.TotalSurcharge;
                ws.Cell(row, 8).Value = (double)p.TotalInterest;
                ws.Cell(row, 9).Value = (double)p.TotalAmount;
                ws.Cell(row, 10).Value = p.DateIssued;
                row++;
            }

            // Totals row
            ws.Cell(row, 1).Value = $"TOTAL — {payments.Count} record(s)";
            ws.Range(row, 1, row, 5).Merge();
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 6).Value = (double)payments.Sum(p => p.TotalBaseAmount);
            ws.Cell(row, 7).Value = (double)payments.Sum(p => p.TotalSurcharge);
            ws.Cell(row, 8).Value = (double)payments.Sum(p => p.TotalInterest);
            ws.Cell(row, 9).Value = (double)payments.Sum(p => p.TotalAmount);
            ws.Range(row, 1, row, 10).Style.Fill.BackgroundColor = XLColor.FromHtml("#E8F5E9");

            ws.Columns(6, 9).Style.NumberFormat.Format = "#,##0.00";
            ws.Columns(1, 10).AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);

            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName + ".xlsx");
        }

        // ── PDF GENERATOR ─────────────────────────────
        private IActionResult GeneratePdf(
            List<PaymentRecord> payments, string fileName,
            string reportType, DateTime from, DateTime to, string generatedBy,
            string? revenueTypeName = null)
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
                        if (!string.IsNullOrEmpty(revenueTypeName))
                        {
                            col.Item().Text($"Revenue Type: {revenueTypeName}")
                                .FontSize(9).AlignCenter();
                        }
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