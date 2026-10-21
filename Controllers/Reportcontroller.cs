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

            // Monthly Remittance renders one column per day, so cap the range at
            // one month to keep the table readable.
            if (type == "monthly-remittance" && (toDate.Date - fromDate.Date).Days > 31)
                return BadRequest("Monthly Remittance accepts a date range of at most 31 days.");

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
                "daily"              => "Daily Collection Report",
                "monthly"            => "Monthly Revenue Summary",
                "annual"             => "Annual Revenue Summary",
                "custom"             => "Custom Report",
                "remittance"         => "Remittance Summary",
                "monthly-remittance" => "Monthly Remittance",
                _                    => "Revenue Summary"
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

            // Remittance reports aggregate line items per revenue type instead of
            // listing transactions, so they use their own data shape and exporters.
            if (type is "remittance" or "monthly-remittance")
            {
                var remittance = BuildRemittance(type, payments, revenueTypeId, fromDate, toDate);
                return normalizedFormat switch
                {
                    "csv"  => GenerateRemittanceCsv(remittance, fileName, reportTypeName, fromDate, toDate, revenueTypeName),
                    "xlsx" => GenerateRemittanceXlsx(remittance, fileName, reportTypeName, fromDate, toDate, revenueTypeName),
                    _      => GenerateRemittancePdf(remittance, fileName, reportTypeName, fromDate, toDate, fullName, revenueTypeName)
                };
            }

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

        // ── REMITTANCE REPORTS ───────────────────────
        private sealed class RemittanceRow
        {
            public string Name { get; init; } = "";
            public decimal Total { get; init; }
            public bool IsSection { get; init; }
            public string RowClass { get; init; } = "";
            public decimal[] Cells { get; init; } = Array.Empty<decimal>();
        }

        private sealed class RemittanceReport
        {
            public bool IsMonthly { get; init; }
            public List<DateTime> Days { get; } = new();
            public List<RemittanceRow> Rows { get; } = new();
            public decimal[] DayTotals { get; set; } = Array.Empty<decimal>();
            public decimal GrandTotal { get; set; }
        }

        // Aggregates already-loaded payments per revenue type. The "remittance"
        // layout splits FEE-category types from everything else (MISCELLANEOUS
        // section); "monthly-remittance" builds a per-day matrix instead.
        private RemittanceReport BuildRemittance(string type, List<PaymentRecord> payments, int? revenueTypeId, DateTime fromDate, DateTime toDate)
        {
            var report = new RemittanceReport { IsMonthly = type == "monthly-remittance" };

            // All active revenue types get a row (even with nothing collected);
            // a type filter narrows the list to that single type, active or not.
            var types = _context.RevenueTypes
                .AsNoTracking()
                .Where(t => revenueTypeId.HasValue ? t.TypeID == revenueTypeId.Value : t.IsActive)
                .ToList();

            var byTypeAndDay = new Dictionary<(int TypeID, DateTime Day), decimal>();
            var byType = new Dictionary<int, decimal>();
            foreach (var p in payments)
            {
                if (p.RecordLineItems is null) continue;
                foreach (var l in p.RecordLineItems)
                {
                    if (revenueTypeId.HasValue && l.TypeID != revenueTypeId.Value) continue;

                    var day = p.DateIssued.Date;
                    byTypeAndDay[(l.TypeID, day)] = byTypeAndDay.GetValueOrDefault((l.TypeID, day)) + l.LineTotal;
                    byType[l.TypeID] = byType.GetValueOrDefault(l.TypeID) + l.LineTotal;
                }
            }

            if (report.IsMonthly)
            {
                // Generate all days in the date range (inclusive) so empty days are displayed
                var fromDay = fromDate.Date;
                var toDay = toDate.Date;
                if (toDay > fromDay.AddDays(31)) toDay = fromDay.AddDays(31); // safety
                for (var d = fromDay; d <= toDay; d = d.AddDays(1))
                {
                    report.Days.Add(d);
                }

                foreach (var t in types.OrderBy(t => t.TypeID))
                {
                    report.Rows.Add(new RemittanceRow
                    {
                        Name = t.Name ?? "",
                        Total = byType.GetValueOrDefault(t.TypeID),
                        RowClass = MonthlyRowClass(t.Name ?? ""),
                        Cells = report.Days.Select(d => byTypeAndDay.GetValueOrDefault((t.TypeID, d))).ToArray()
                    });
                }

                report.DayTotals = report.Days
                    .Select((_, i) => report.Rows.Sum(r => r.Cells[i]))
                    .ToArray();
                report.GrandTotal = report.Rows.Sum(r => r.Total);
            }
            else
            {
                var ordered = types
                    .OrderBy(t => IsFeeCategory(t.CategoryID) ? 0 : 1)
                    .ThenBy(t => t.TypeID)
                    .ToList();

                var sectionInserted = false;
                foreach (var t in ordered)
                {
                    var isMisc = !IsFeeCategory(t.CategoryID);
                    if (isMisc && !sectionInserted)
                    {
                        report.Rows.Add(new RemittanceRow { Name = "MISCELLANEOUS:", IsSection = true });
                        sectionInserted = true;
                    }
                    report.Rows.Add(new RemittanceRow
                    {
                        Name = t.Name ?? "",
                        Total = byType.GetValueOrDefault(t.TypeID)
                    });
                }

                report.GrandTotal = report.Rows.Where(r => !r.IsSection).Sum(r => r.Total);
            }

            return report;
        }

        private static bool IsFeeCategory(string? categoryId) =>
            string.Equals(categoryId, "FEE", StringComparison.OrdinalIgnoreCase);

        private static string MonthlyRowClass(string name)
        {
            if (name.Contains("business tax", StringComparison.OrdinalIgnoreCase)) return "bt";
            if (name.Contains("kabutungan", StringComparison.OrdinalIgnoreCase)) return "kb";
            if (name.Contains("inambakan", StringComparison.OrdinalIgnoreCase)) return "in";
            if (name.Contains("cock", StringComparison.OrdinalIgnoreCase)) return "cf";
            return "";
        }

        private static string AmountOrBlank(decimal value) =>
            value.ToString("N2", CultureInfo.InvariantCulture);

        private static string AmountOrBlankCsv(decimal value) =>
            value.ToString("F2", CultureInfo.InvariantCulture);

        private static string DayHeader(DateTime day)
        {
            var month = day.ToString("MMM", CultureInfo.InvariantCulture).ToUpperInvariant();
            if (month == "SEP") month = "SEPT";   // September abbreviates differently
            return $"{month}. {day.ToString("dd", CultureInfo.InvariantCulture)}, {day.ToString("yyyy", CultureInfo.InvariantCulture)}";
        }

        private static string MonthlyPeriodTitle(DateTime from, DateTime to) =>
            from.Month == to.Month && from.Year == to.Year
                ? from.ToString("MMM-yy", CultureInfo.InvariantCulture)
                : $"{from.ToString("MMM-yy", CultureInfo.InvariantCulture)} - {to.ToString("MMM-yy", CultureInfo.InvariantCulture)}";

        private IActionResult GenerateRemittanceCsv(
            RemittanceReport report, string fileName,
            string reportType, DateTime from, DateTime to, string? revenueTypeName)
        {
            var sb = new StringBuilder();
            sb.AppendLine("LGU TREASURER'S OFFICE COLLECTION RECORDING SYSTEM");
            sb.AppendLine($"Report Type: {reportType}");
            if (!string.IsNullOrEmpty(revenueTypeName))
                sb.AppendLine($"Revenue Type: {revenueTypeName}");
            sb.AppendLine($"Period: {from:MMMM dd, yyyy} to {to:MMMM dd, yyyy}");
            if (report.IsMonthly)
                sb.AppendLine("GENERAL COLLECTIONS");
            sb.AppendLine($"Generated: {DateTime.Now:MMMM dd, yyyy hh:mm tt}");
            sb.AppendLine();

            if (!report.IsMonthly)
            {
                sb.AppendLine("Item,Amount");
                foreach (var row in report.Rows)
                {
                    sb.AppendLine(row.IsSection
                        ? CsvField(row.Name) + ","
                        : CsvField(row.Name) + "," + AmountOrBlankCsv(row.Total));
                }
                sb.AppendLine("TOTAL," + report.GrandTotal.ToString("F2", CultureInfo.InvariantCulture));
            }
            else
            {
                var header = new StringBuilder("Item");
                foreach (var day in report.Days)
                    header.Append(',').Append(CsvField(DayHeader(day)));
                header.Append(",TOTAL");
                sb.AppendLine(header.ToString());

                foreach (var row in report.Rows)
                {
                    if (row.IsSection)
                    {
                        sb.AppendLine(CsvField(row.Name) + ",");
                        continue;
                    }
                    var line = new StringBuilder(CsvField(row.Name));
                    foreach (var cell in row.Cells)
                        line.Append(',').Append(AmountOrBlankCsv(cell));
                    line.Append(',').Append(AmountOrBlankCsv(row.Total));
                    sb.AppendLine(line.ToString());
                }

                var totals = new StringBuilder("Total");
                foreach (var total in report.DayTotals)
                    totals.Append(',').Append(AmountOrBlankCsv(total));
                totals.Append(',').Append(AmountOrBlankCsv(report.GrandTotal));
                sb.AppendLine(totals.ToString());
            }

            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "text/csv", fileName + ".csv");
        }

        private IActionResult GenerateRemittanceXlsx(
            RemittanceReport report, string fileName,
            string reportType, DateTime from, DateTime to, string? revenueTypeName)
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add(report.IsMonthly ? "Monthly Remittance" : "Remittance Summary");
            ws.Style.Font.FontName = "Arial";
            ws.Style.Font.FontSize = 10;

            var colCount = report.IsMonthly ? report.Days.Count + 2 : 2;

            static void TitleRow(IXLWorksheet sheet, int row, int lastCol, string text, int size, bool bold)
            {
                sheet.Cell(row, 1).Value = text;
                sheet.Range(row, 1, row, lastCol).Merge();
                sheet.Cell(row, 1).Style.Font.Bold = bold;
                sheet.Cell(row, 1).Style.Font.FontSize = size;
                sheet.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            static void ThinBorder(IXLRange range)
            {
                var border = range.Style.Border;
                border.TopBorder = XLBorderStyleValues.Thin;
                border.BottomBorder = XLBorderStyleValues.Thin;
                border.LeftBorder = XLBorderStyleValues.Thin;
                border.RightBorder = XLBorderStyleValues.Thin;
            }

            TitleRow(ws, 1, colCount, "LGU TREASURER'S OFFICE COLLECTION RECORDING SYSTEM", 14, true);
            TitleRow(ws, 2, colCount, reportType, 12, true);
            TitleRow(ws, 3, colCount, $"Period: {from:MMMM dd, yyyy} to {to:MMMM dd, yyyy}", 10, false);

            var headerRow = 4;
            if (report.IsMonthly)
            {
                TitleRow(ws, headerRow, colCount, MonthlyPeriodTitle(from, to), 12, true);
                headerRow++;
                TitleRow(ws, headerRow, colCount, "GENERAL COLLECTIONS", 10, true);
                headerRow++;
            }
            if (!string.IsNullOrEmpty(revenueTypeName))
            {
                TitleRow(ws, headerRow, colCount, $"Revenue Type: {revenueTypeName}", 10, false);
                headerRow++;
            }

            if (!report.IsMonthly)
            {
                ws.Cell(headerRow, 1).Value = "Item";
                ws.Cell(headerRow, 2).Value = "Amount (₱)";
                ws.Range(headerRow, 1, headerRow, 2).Style.Font.Bold = true;
                ws.Range(headerRow, 1, headerRow, 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#EEF1F7");
                ws.Range(headerRow, 1, headerRow, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ThinBorder(ws.Range(headerRow, 1, headerRow, 2));

                var row = headerRow + 1;
                foreach (var r in report.Rows)
                {
                    if (r.IsSection)
                    {
                        ws.Cell(row, 1).Value = r.Name;
                        ws.Range(row, 1, row, 2).Merge();
                        ws.Cell(row, 1).Style.Font.Bold = true;
                        ws.Range(row, 1, row, 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#EEF1F7");
                    }
                    else
                    {
                        ws.Cell(row, 1).Value = r.Name;
                        if (r.Total != 0)
                            ws.Cell(row, 2).Value = (double)r.Total;
                        ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
                        ws.Cell(row, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                    }
                    ThinBorder(ws.Range(row, 1, row, 2));
                    row++;
                }

                ws.Cell(row, 1).Value = "TOTAL";
                ws.Cell(row, 2).Value = (double)report.GrandTotal;
                ws.Cell(row, 2).Style.NumberFormat.Format = "#,##0.00";
                ws.Cell(row, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                ws.Range(row, 1, row, 2).Style.Font.Bold = true;
                ws.Range(row, 1, row, 2).Style.Fill.BackgroundColor = XLColor.FromHtml("#DFF0D0");
                ThinBorder(ws.Range(row, 1, row, 2));

                ws.Columns(1, 2).AdjustToContents();
            }
            else
            {
                ws.Cell(headerRow, 1).Value = "";
                for (var i = 0; i < report.Days.Count; i++)
                    ws.Cell(headerRow, 2 + i).Value = DayHeader(report.Days[i]);
                ws.Cell(headerRow, colCount).Value = "TOTAL";
                var headRange = ws.Range(headerRow, 1, headerRow, colCount);
                headRange.Style.Font.Bold = true;
                headRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#F0F2F6");
                headRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ThinBorder(headRange);

                var row = headerRow + 1;
                foreach (var r in report.Rows)
                {
                    ws.Cell(row, 1).Value = r.Name;
                    ws.Cell(row, 1).Style.Font.Bold = true;

                    switch (r.RowClass)
                    {
                        case "bt":
                            ws.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#4A86E8");
                            ws.Cell(row, 1).Style.Font.FontColor = XLColor.FromHtml("#FFFFFF");
                            break;
                        case "kb":
                            ws.Range(row, 1, row, colCount).Style.Fill.BackgroundColor = XLColor.FromHtml("#8BC34A");
                            break;
                        case "in":
                            ws.Range(row, 1, row, colCount).Style.Fill.BackgroundColor = XLColor.FromHtml("#4FB0F0");
                            break;
                        case "cf":
                            ws.Range(row, 1, row, colCount).Style.Fill.BackgroundColor = XLColor.FromHtml("#FFD54A");
                            break;
                    }

                    for (var i = 0; i < report.Days.Count; i++)
                    {
                        if (r.Cells[i] != 0)
                            ws.Cell(row, 2 + i).Value = (double)r.Cells[i];
                    }
                    if (r.Total != 0)
                        ws.Cell(row, colCount).Value = (double)r.Total;
                    ws.Range(row, 2, row, colCount).Style.NumberFormat.Format = "#,##0.00";
                    ws.Range(row, 2, row, colCount).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                    ws.Cell(row, colCount).Style.Font.FontColor = XLColor.FromHtml("#C0392B");
                    ws.Cell(row, colCount).Style.Font.Bold = true;
                    ThinBorder(ws.Range(row, 1, row, colCount));
                    row++;
                }

                ws.Cell(row, 1).Value = "Total";
                for (var i = 0; i < report.DayTotals.Length; i++)
                {
                    if (report.DayTotals[i] != 0)
                        ws.Cell(row, 2 + i).Value = (double)report.DayTotals[i];
                }
                if (report.GrandTotal != 0)
                    ws.Cell(row, colCount).Value = (double)report.GrandTotal;
                var totRange = ws.Range(row, 1, row, colCount);
                totRange.Style.Font.Bold = true;
                totRange.Style.Font.FontColor = XLColor.FromHtml("#C0392B");
                totRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#F0F2F6");
                ws.Range(row, 2, row, colCount).Style.NumberFormat.Format = "#,##0.00";
                ws.Range(row, 2, row, colCount).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                ThinBorder(totRange);

                ws.Column(1).Width = 34;
                if (report.Days.Count > 0)
                    ws.Columns(2, colCount).AdjustToContents();
                ws.SheetView.FreezeRows(headerRow);
                ws.SheetView.FreezeColumns(1);
            }

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);

            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName + ".xlsx");
        }

        private IActionResult GenerateRemittancePdf(
            RemittanceReport report, string fileName,
            string reportType, DateTime from, DateTime to, string generatedBy,
            string? revenueTypeName)
        {
            var document = Document.Create(container => container.Page(page =>
            {
                if (report.IsMonthly)
                {
                    // Wide landscape: item column + one column per day + TOTAL
                    var width = 40f + 150f + 64f + report.Days.Count * 50f;
                    page.Size(new PageSize(Math.Max(width, 842f), 595f));
                }
                else
                {
                    page.Size(PageSizes.A4.Portrait());
                }
                page.Margin(20);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));

                page.Header().Column(col =>
                {
                    col.Item().Text("LGU TREASURER'S OFFICE COLLECTION RECORDING SYSTEM")
                        .Bold().FontSize(14).AlignCenter();
                    col.Item().Text(reportType).Bold().FontSize(11).AlignCenter();
                    if (report.IsMonthly)
                    {
                        col.Item().Text(MonthlyPeriodTitle(from, to)).Bold().FontSize(12).AlignCenter();
                        col.Item().Text("GENERAL COLLECTIONS").Bold().FontSize(10).AlignCenter();
                    }
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
                    if (!report.IsMonthly)
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn();
                            cols.ConstantColumn(120);
                        });

                        static IContainer SCell(IContainer c, string? bg = null)
                        {
                            c = c.Border(1).BorderColor("#D0D4DE").Padding(5);
                            if (!string.IsNullOrEmpty(bg)) c = c.Background(bg);
                            return c;
                        }

                        table.Header(h =>
                        {
                            h.Cell().Element(c => SCell(c, "#EEF1F7")).Text("Item").Bold();
                            h.Cell().Element(c => SCell(c, "#EEF1F7")).AlignRight().Text("Amount (₱)").Bold();
                        });

                        foreach (var row in report.Rows)
                        {
                            if (row.IsSection)
                            {
                                table.Cell().ColumnSpan(2).Element(c => SCell(c, "#EEF1F7"))
                                    .Text(row.Name).Bold();
                                continue;
                            }
                            table.Cell().Element(c => SCell(c)).Text(row.Name);
                            table.Cell().Element(c => SCell(c)).AlignRight().Text(AmountOrBlank(row.Total));
                        }

                        table.Cell().Element(c => SCell(c, "#DFF0D0")).Text("TOTAL").Bold();
                        table.Cell().Element(c => SCell(c, "#DFF0D0")).AlignRight()
                            .Text(report.GrandTotal.ToString("N2", CultureInfo.InvariantCulture)).Bold();
                    }
                    else
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(150);   // revenue type
                            foreach (var _ in report.Days)
                                cols.ConstantColumn(50);
                            cols.ConstantColumn(64);    // TOTAL
                        });

                        static IContainer MCell(IContainer c, string? bg = null)
                        {
                            c = c.Border(1).BorderColor("#99AAAA").Padding(3);
                            if (!string.IsNullOrEmpty(bg)) c = c.Background(bg);
                            return c;
                        }

                        static IContainer MTot(IContainer c) =>
                            c.Border(1).BorderColor("#99AAAA").Background("#F0F2F6").Padding(3);

                        table.Header(h =>
                        {
                            h.Cell().Element(c => MCell(c, "#F0F2F6")).Text("").FontSize(7);
                            foreach (var day in report.Days)
                            {
                                h.Cell().Element(c => MCell(c, "#F0F2F6"))
                                    .Text(DayHeader(day)).Bold().FontSize(6);
                            }
                            h.Cell().Element(c => MCell(c, "#F0F2F6"))
                                .Text("TOTAL").Bold().FontSize(7);
                        });

                        foreach (var row in report.Rows)
                        {
                            var isBt = row.RowClass == "bt";
                            var rowBg = row.RowClass switch
                            {
                                "kb" => "#8BC34A",
                                "in" => "#4FB0F0",
                                "cf" => "#FFD54A",
                                _   => (string?)null
                            };
                            var nameBg = isBt ? "#4A86E8" : rowBg;

                            var nameText = table.Cell().Element(c => MCell(c, nameBg))
                                .Text(row.Name).Bold().FontSize(7);
                            if (isBt) nameText.FontColor("#FFFFFF");

                            for (var i = 0; i < report.Days.Count; i++)
                            {
                                table.Cell().Element(c => MCell(c, rowBg))
                                    .AlignRight().Text(AmountOrBlank(row.Cells[i])).FontSize(7);
                            }

                            table.Cell().Element(c => MCell(c, rowBg)).AlignRight()
                                .Text(AmountOrBlank(row.Total))
                                .Bold().FontSize(7).FontColor("#C0392B");
                        }

                        table.Cell().Element(MTot).Text("Total").Bold().FontSize(7).FontColor("#C0392B");
                        foreach (var total in report.DayTotals)
                        {
                            table.Cell().Element(MTot).AlignRight().Text(AmountOrBlank(total))
                                .Bold().FontSize(7).FontColor("#C0392B");
                        }
                        table.Cell().Element(MTot).AlignRight().Text(AmountOrBlank(report.GrandTotal))
                            .Bold().FontSize(7).FontColor("#C0392B");
                    }
                });

                page.Footer().AlignRight().Text(text =>
                {
                    text.Span($"Generated by: {generatedBy}  |  ").FontSize(8);
                    text.Span($"{DateTime.Now:MMMM dd, yyyy hh:mm tt}  |  Page ").FontSize(8);
                    text.CurrentPageNumber().FontSize(8);
                    text.Span(" of ").FontSize(8);
                    text.TotalPages().FontSize(8);
                });
            }));

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