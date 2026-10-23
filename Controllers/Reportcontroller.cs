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

        // GET: /Report/Index  (unchanged)
        public async Task<IActionResult> Index()
        {
            var userID = HttpContext.Session.GetInt32("UserID");
            if (userID == null)
                return RedirectToAction("Login", "Account");

            ViewData["ActiveNav"] = "report";
            ViewData["FullName"] = HttpContext.Session.GetString("FullName");
            ViewData["Role"] = HttpContext.Session.GetString("Role");
            ViewData["Initials"] = GetInitials(ViewData["FullName"]?.ToString());

            ViewBag.RecentReports = await _context.ReportLog
                .OrderByDescending(r => r.GeneratedAt)
                .Take(10)
                .ToListAsync();

            ViewBag.RevenueTypes = await _context.RevenueTypes
                .OrderBy(r => r.Name)
                .Select(r => new { r.TypeID, r.Name, r.IsActive })
                .ToListAsync();

            return View();
        }

        // GET: /Report/Generate
        public async Task<IActionResult> Generate(string type, string format, string from, string to, int? revenueTypeId)
        {
            var userID = HttpContext.Session.GetInt32("UserID");
            if (userID == null)
                return RedirectToAction("Login", "Account");

            var normalizedFormat = (format ?? string.Empty).Trim().ToLowerInvariant();
            if (normalizedFormat is not ("pdf" or "csv" or "xlsx"))
                return BadRequest("Unsupported file format. Use 'pdf', 'csv', or 'xlsx'.");

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

            // Normalize the range to whole days
            fromDate = fromDate.Date;
            toDate = toDate.Date.AddDays(1).AddSeconds(-1);

            // Payments (with line items) in the date range
            var query = _context.PaymentRecords
                .Include(p => p.Payee)
                .Include(p => p.RecordLineItems)
                    .ThenInclude(l => l.RevenueType)
                .Where(p => p.DateIssued >= fromDate && p.DateIssued <= toDate);

            if (revenueTypeId.HasValue)
                query = query.Where(p => p.RecordLineItems.Any(l => l.TypeID == revenueTypeId.Value));

            var payments = await query
                .OrderBy(p => p.DateIssued)
                .ToListAsync();

            // All revenue types become the rows of the report
            var revenueTypes = await _context.RevenueTypes
                .OrderBy(r => r.Name)
                .ToListAsync();

            var matrix = BuildMatrix(payments, revenueTypes, fromDate, toDate, revenueTypeId);

            var reportTypeName = type switch
            {
                "daily"              => "Daily Collection Report",
                "monthly"            => "Monthly Revenue Summary",
                "annual"             => "Annual Revenue Summary",
                "custom"             => "Custom Report",
                "remittance"         => "Remittance Summary",
                "monthly-remittance" => "Monthly Remittance",
                "detailed"           => "Detailed Collection List",
                _                    => "Revenue Summary"
            };

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

            // The detailed list keeps the individual transactions behind the
            // summary matrix (OR number, payor, method, and the
            // base/surcharge/interest breakdown).
            if (type == "detailed")
            {
                return normalizedFormat switch
                {
                    "csv"  => GenerateDetailedCsv(payments, fileName, reportTypeName, fromDate, toDate, revenueTypeName),
                    "xlsx" => GenerateDetailedXlsx(payments, fileName, reportTypeName, fromDate, toDate, revenueTypeName),
                    _      => GenerateDetailedPdf(payments, fileName, reportTypeName, fromDate, toDate, fullName, revenueTypeName)
                };
            }

            // Generate and return the file
            return normalizedFormat switch
            {
                "csv"  => GenerateCsv(matrix, fileName, reportTypeName, fromDate, toDate, revenueTypeName),
                "xlsx" => GenerateXlsx(matrix, fileName, reportTypeName, fromDate, toDate, revenueTypeName),
                _      => GeneratePdf(matrix, fileName, reportTypeName, fromDate, toDate, fullName, revenueTypeName)
            };
        }

        // ── MATRIX (rows = revenue types, columns = dates, last column = TOTAL) ──

        private sealed class Matrix
        {
            public List<(DateTime Start, string Label)> Columns { get; } = new();
            public List<(int TypeID, string Name)> Rows { get; } = new();
            public Dictionary<(int TypeID, DateTime Bucket), decimal> Cells { get; } = new();

            public decimal Cell(int typeId, DateTime bucket) =>
                Cells.TryGetValue((typeId, bucket), out var v) ? v : 0m;

            public decimal RowTotal(int typeId) => Columns.Sum(c => Cell(typeId, c.Start));
            public decimal ColTotal(DateTime bucket) => Rows.Sum(r => Cell(r.TypeID, bucket));
            public decimal GrandTotal => Rows.Sum(r => RowTotal(r.TypeID));
        }

        // The amount a line item contributes to the report.
        // LineTotal is base + surcharge + interest (set in RecordController when the
        // line item is saved), so it matches PaymentRecord.TotalAmount and the
        // remittance reports. Using BaseAmount here would drop surcharge and
        // interest and make this report disagree with the remittance reports.
        private static decimal LineAmount(RecordLineItem l) => l.LineTotal;

        private static Matrix BuildMatrix(
            List<PaymentRecord> payments, List<RevenueType> types,
            DateTime from, DateTime to, int? revenueTypeId)
        {
            // Up to ~31 days: one column per day (like the paper sheet).
            // Longer ranges (monthly/annual/custom): one column per month.
            var byMonth = (to.Date - from.Date).TotalDays > 31;
            DateTime Bucket(DateTime d) => byMonth ? new DateTime(d.Year, d.Month, 1) : d.Date;

            var m = new Matrix();

            foreach (var p in payments)
            {
                foreach (var l in p.RecordLineItems ?? Enumerable.Empty<RecordLineItem>())
                {
                    if (revenueTypeId.HasValue && l.TypeID != revenueTypeId.Value)
                        continue;

                    var key = (l.TypeID, Bucket(p.DateIssued));
                    m.Cells[key] = m.Cell(key.TypeID, key.Item2) + LineAmount(l);
                }
            }

            // Columns
            if (byMonth)
            {
                for (var d = new DateTime(from.Year, from.Month, 1); d <= to; d = d.AddMonths(1))
                    m.Columns.Add((d, d.ToString("MMM yyyy", CultureInfo.InvariantCulture).ToUpper()));
            }
           else
{
    // Every weekday in the range, plus any weekend date that actually has collections
    var dataDates = m.Cells.Keys.Select(k => k.Bucket).ToHashSet();
    for (var d = from.Date; d <= to.Date; d = d.AddDays(1))
    {
        var weekend = d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday;
        if (!weekend || dataDates.Contains(d))
            m.Columns.Add((d, d.ToString("MMM dd, yyyy", CultureInfo.InvariantCulture).ToUpper()));
    }
}

            // Rows: every active type (even with no collections, like the paper sheet);
            // inactive types only if they actually have collections in the range.
            foreach (var t in types)
            {
                if (revenueTypeId.HasValue && t.TypeID != revenueTypeId.Value) continue;
                var hasData = m.Cells.Keys.Any(k => k.TypeID == t.TypeID);
                if (t.IsActive || hasData)
                    m.Rows.Add((t.TypeID, t.Name ?? ""));
            }

            return m;
        }

        // blank for zero, like the printed sheet
        private static string Amt(decimal v) =>
            v == 0 ? "" : v.ToString("N2", CultureInfo.InvariantCulture);

        // ── CSV ───────────────────────────────────────
        private IActionResult GenerateCsv(
            Matrix m, string fileName, string reportType,
            DateTime from, DateTime to, string? revenueTypeName = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine(CsvField("LGU TREASURER'S OFFICE COLLECTION RECORDING SYSTEM"));
            sb.AppendLine(CsvField($"Report Type: {reportType}"));
            if (!string.IsNullOrEmpty(revenueTypeName))
                sb.AppendLine(CsvField($"Revenue Type: {revenueTypeName}"));
            sb.AppendLine(CsvField($"Period: {from:MMMM dd, yyyy} to {to:MMMM dd, yyyy}"));
            sb.AppendLine(CsvField($"Generated: {DateTime.Now:MMMM dd, yyyy hh:mm tt}"));
            sb.AppendLine();

            // Header
            sb.AppendLine(string.Join(",",
                new[] { CsvField("Revenue Type") }
                    .Concat(m.Columns.Select(c => CsvField(c.Label)))
                    .Append(CsvField("TOTAL"))));

            // One row per revenue type (plain numbers, no thousands separators, so Excel reads them as numbers)
            foreach (var r in m.Rows)
            {
                var cells = new List<string> { CsvField(r.Name) };
                foreach (var c in m.Columns)
                {
                    var v = m.Cell(r.TypeID, c.Start);
                    cells.Add(v == 0 ? "" : v.ToString("0.00", CultureInfo.InvariantCulture));
                }
                cells.Add(m.RowTotal(r.TypeID).ToString("0.00", CultureInfo.InvariantCulture));
                sb.AppendLine(string.Join(",", cells));
            }

            // Total row
            var totals = new List<string> { CsvField("TOTAL") };
            totals.AddRange(m.Columns.Select(c => m.ColTotal(c.Start).ToString("0.00", CultureInfo.InvariantCulture)));
            totals.Add(m.GrandTotal.ToString("0.00", CultureInfo.InvariantCulture));
            sb.AppendLine(string.Join(",", totals));

            // UTF-8 with BOM so Excel shows characters correctly
            var bytes = new UTF8Encoding(true).GetPreamble()
                .Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
            return File(bytes, "text/csv", fileName + ".csv");
        }

        // Escapes a value for CSV and neutralizes spreadsheet formula injection.
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

        // ── EXCEL (XLSX) ──────────────────────────────
        private IActionResult GenerateXlsx(
            Matrix m, string fileName, string reportType,
            DateTime from, DateTime to, string? revenueTypeName = null)
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Revenue Summary");
            ws.Style.Font.FontName = "Arial";
            ws.Style.Font.FontSize = 10;

            var totalCols = m.Columns.Count + 2;   // name + dates + TOTAL

            void Title(int row, string text, bool bold, int size)
            {
                ws.Cell(row, 1).Value = text;
                ws.Range(row, 1, row, totalCols).Merge();
                ws.Cell(row, 1).Style.Font.Bold = bold;
                ws.Cell(row, 1).Style.Font.FontSize = size;
                ws.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            Title(1, "LGU TREASURER'S OFFICE COLLECTION RECORDING SYSTEM", true, 14);
            Title(2, reportType, true, 12);
            Title(3, $"Period: {from:MMMM dd, yyyy} to {to:MMMM dd, yyyy}", false, 10);

            var headerRow = 4;
            if (!string.IsNullOrEmpty(revenueTypeName))
            {
                Title(4, $"Revenue Type: {revenueTypeName}", false, 10);
                headerRow = 5;
            }

            // Header row
            ws.Cell(headerRow, 1).Value = "GENERAL COLLECTIONS";
            for (var i = 0; i < m.Columns.Count; i++)
                ws.Cell(headerRow, i + 2).Value = m.Columns[i].Label;
            ws.Cell(headerRow, totalCols).Value = "TOTAL";

            var header = ws.Range(headerRow, 1, headerRow, totalCols);
            header.Style.Font.Bold = true;
            header.Style.Fill.BackgroundColor = XLColor.FromHtml("#C8E6C9");
            header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            header.Style.Alignment.WrapText = true;

            // Body: one row per revenue type
            var row = headerRow + 1;
            foreach (var r in m.Rows)
            {
                ws.Cell(row, 1).Value = r.Name;
                ws.Cell(row, 1).Style.Font.Bold = true;

                for (var i = 0; i < m.Columns.Count; i++)
                {
                    var v = m.Cell(r.TypeID, m.Columns[i].Start);
                    if (v != 0) ws.Cell(row, i + 2).Value = (double)v;
                }

                var rt = m.RowTotal(r.TypeID);
                if (rt != 0) ws.Cell(row, totalCols).Value = (double)rt;
                ws.Cell(row, totalCols).Style.Font.Bold = true;
                row++;
            }

            // Total row
            ws.Cell(row, 1).Value = "TOTAL";
            for (var i = 0; i < m.Columns.Count; i++)
                ws.Cell(row, i + 2).Value = (double)m.ColTotal(m.Columns[i].Start);
            ws.Cell(row, totalCols).Value = (double)m.GrandTotal;

            var totalRange = ws.Range(row, 1, row, totalCols);
            totalRange.Style.Font.Bold = true;
            totalRange.Style.Font.FontColor = XLColor.FromHtml("#C62828");
            totalRange.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8F5E9");

            // Grid + formatting
            var table = ws.Range(headerRow, 1, row, totalCols);
            table.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            table.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            ws.Range(headerRow + 1, 2, row, totalCols).Style.NumberFormat.Format = "#,##0.00";
            ws.Range(headerRow + 1, 2, row, totalCols).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            ws.Column(1).Width = 34;
            for (var c = 2; c <= totalCols; c++) ws.Column(c).Width = 13;

            ws.SheetView.Freeze(headerRow, 1);   // keep header + names visible when scrolling
            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            ws.PageSetup.FitToPages(1, 0);

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);

            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName + ".xlsx");
        }

        // ── PDF ───────────────────────────────────────
        private IActionResult GeneratePdf(
            Matrix m, string fileName, string reportType,
            DateTime from, DateTime to, string generatedBy,
            string? revenueTypeName = null)
        {
            var wide = m.Columns.Count > 12;
            var fontSize = m.Columns.Count > 24 ? 6f : 7f;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(wide ? PageSizes.A3.Landscape() : PageSizes.A4.Landscape());
                    page.Margin(20);

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
                            cols.ConstantColumn(wide ? 130 : 110);            // revenue type
                            foreach (var _ in m.Columns) cols.RelativeColumn(); // one per date
                            cols.ConstantColumn(wide ? 70 : 62);              // TOTAL
                        });

                        static IContainer HeaderCell(IContainer c) =>
                            c.Border(0.5f).BorderColor("#9E9E9E").Background("#C8E6C9").Padding(3).AlignCenter().AlignMiddle();
                        static IContainer BodyCell(IContainer c) =>
                            c.Border(0.5f).BorderColor("#9E9E9E").Padding(3);
                        static IContainer TotalCell(IContainer c) =>
                            c.Border(0.5f).BorderColor("#9E9E9E").Background("#E8F5E9").Padding(3);

                        table.Header(h =>
                        {
                            h.Cell().Element(HeaderCell).Text("GENERAL COLLECTIONS").Bold().FontSize(fontSize);
                            foreach (var c in m.Columns)
                                h.Cell().Element(HeaderCell).Text(c.Label).Bold().FontSize(fontSize);
                            h.Cell().Element(HeaderCell).Text("TOTAL").Bold().FontSize(fontSize);
                        });

                        foreach (var r in m.Rows)
                        {
                            table.Cell().Element(BodyCell).Text(r.Name).Bold().FontSize(fontSize);
                            foreach (var c in m.Columns)
                                table.Cell().Element(BodyCell).AlignRight()
                                    .Text(Amt(m.Cell(r.TypeID, c.Start))).FontSize(fontSize);
                            table.Cell().Element(BodyCell).AlignRight()
                                .Text(Amt(m.RowTotal(r.TypeID))).Bold().FontSize(fontSize);
                        }

                        // Total row (red, like the printed sheet)
                        table.Cell().Element(TotalCell).Text("TOTAL").Bold().FontSize(fontSize).FontColor("#C62828");
                        foreach (var c in m.Columns)
                            table.Cell().Element(TotalCell).AlignRight()
                                .Text(m.ColTotal(c.Start).ToString("N2", CultureInfo.InvariantCulture))
                                .Bold().FontSize(fontSize).FontColor("#C62828");
                        table.Cell().Element(TotalCell).AlignRight()
                            .Text(m.GrandTotal.ToString("N2", CultureInfo.InvariantCulture))
                            .Bold().FontSize(fontSize).FontColor("#C62828");
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

        // ── DETAILED COLLECTION LIST ───────────────────────────────
        // The summary reports above are matrices (revenue type x date). This one
        // lists the individual transactions behind those totals, so the OR
        // number, payor, payment method and the base/surcharge/interest
        // breakdown stay exportable for reconciliation and audit.

        private static string DetailPayee(PaymentRecord p) =>
            p.Payee != null ? $"{p.Payee.Lastname}, {p.Payee.Firstname}" : "—";

        private static string DetailType(PaymentRecord p) =>
            p.RecordLineItems?.FirstOrDefault()?.RevenueType?.Name ?? "—";

        private static string DetailMethod(PaymentRecord p) =>
            p.PaymentMethod ?? "—";

        private IActionResult GenerateDetailedCsv(
            List<PaymentRecord> payments, string fileName,
            string reportType, DateTime from, DateTime to, string? revenueTypeName = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine(CsvField("LGU TREASURER'S OFFICE COLLECTION RECORDING SYSTEM"));
            sb.AppendLine(CsvField($"Report Type: {reportType}"));
            if (!string.IsNullOrEmpty(revenueTypeName))
                sb.AppendLine(CsvField($"Revenue Type: {revenueTypeName}"));
            sb.AppendLine(CsvField($"Period: {from:MMMM dd, yyyy} to {to:MMMM dd, yyyy}"));
            sb.AppendLine(CsvField($"Generated: {DateTime.Now:MMMM dd, yyyy hh:mm tt}"));
            sb.AppendLine();

            sb.AppendLine(string.Join(",", new[]
            {
                CsvField("Transaction ID"), CsvField("OR Number"), CsvField("Payor"),
                CsvField("Collection Type"), CsvField("Payment Method"),
                CsvField("Base Amount"), CsvField("Surcharge"), CsvField("Interest"),
                CsvField("Total Amount"), CsvField("Date Issued")
            }));

            foreach (var p in payments)
            {
                sb.AppendLine(string.Join(",", new[]
                {
                    CsvField("TXN-" + p.PaymentID.ToString("D6")),
                    CsvField(p.OfficialReceipt),
                    CsvField(DetailPayee(p)),
                    CsvField(DetailType(p)),
                    CsvField(DetailMethod(p)),
                    AmountOrBlankCsv(p.TotalBaseAmount),
                    AmountOrBlankCsv(p.TotalSurcharge),
                    AmountOrBlankCsv(p.TotalInterest),
                    AmountOrBlankCsv(p.TotalAmount),
                    CsvField(p.DateIssued.ToString("yyyy-MM-dd hh:mm tt", CultureInfo.InvariantCulture))
                }));
            }

            sb.AppendLine();
            sb.AppendLine(CsvField($"Total Records: {payments.Count}"));
            sb.AppendLine(CsvField($"Total Collected: {AmountOrBlank(payments.Sum(p => p.TotalAmount))}"));

            // UTF-8 with BOM so Excel shows characters correctly
            var bytes = new UTF8Encoding(true).GetPreamble()
                .Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
            return File(bytes, "text/csv", fileName + ".csv");
        }

        private IActionResult GenerateDetailedXlsx(
            List<PaymentRecord> payments, string fileName,
            string reportType, DateTime from, DateTime to, string? revenueTypeName = null)
        {
            using var workbook = new XLWorkbook();
            var ws = workbook.Worksheets.Add("Collection List");
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
                ws.Cell(row, 1).Value = "TXN-" + p.PaymentID.ToString("D6");
                ws.Cell(row, 2).Value = p.OfficialReceipt ?? "—";
                ws.Cell(row, 3).Value = DetailPayee(p);
                ws.Cell(row, 4).Value = DetailType(p);
                ws.Cell(row, 5).Value = DetailMethod(p);
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

        private IActionResult GenerateDetailedPdf(
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
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Lato"));

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

                            IContainer Cell(IContainer c) => c.Background(bg).Padding(4);

                            table.Cell().Element(Cell).Text("TXN-" + p.PaymentID.ToString("D6")).FontSize(8);
                            table.Cell().Element(Cell).Text(p.OfficialReceipt ?? "—").FontSize(8);
                            table.Cell().Element(Cell).Text(DetailPayee(p)).FontSize(8);
                            table.Cell().Element(Cell).Text(DetailType(p)).FontSize(8);
                            table.Cell().Element(Cell).Text(DetailMethod(p)).FontSize(8);
                            table.Cell().Element(Cell).AlignRight().Text($"{p.TotalBaseAmount:N2}").FontSize(8);
                            table.Cell().Element(Cell).AlignRight().Text($"{p.TotalSurcharge:N2}").FontSize(8);
                            table.Cell().Element(Cell).AlignRight().Text($"{p.TotalInterest:N2}").FontSize(8);
                            table.Cell().Element(Cell).AlignRight().Text($"{p.TotalAmount:N2}").Bold().FontSize(8);
                            table.Cell().Element(Cell).Text(p.DateIssued.ToString("MM/dd/yyyy")).FontSize(8);
                        }

                        IContainer TotalCell(IContainer c) => c.Background("#e8f5e9").Padding(4);

                        table.Cell().ColumnSpan(5).Element(TotalCell)
                            .Text($"TOTAL — {payments.Count} record(s)").Bold().FontSize(8);
                        table.Cell().Element(TotalCell).AlignRight()
                            .Text($"{payments.Sum(p => p.TotalBaseAmount):N2}").Bold().FontSize(8);
                        table.Cell().Element(TotalCell).AlignRight()
                            .Text($"{payments.Sum(p => p.TotalSurcharge):N2}").Bold().FontSize(8);
                        table.Cell().Element(TotalCell).AlignRight()
                            .Text($"{payments.Sum(p => p.TotalInterest):N2}").Bold().FontSize(8);
                        table.Cell().Element(TotalCell).AlignRight()
                            .Text($"{totalAmount:N2}").Bold().FontSize(8);
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
        // layout groups types under their revenue category heading;
        // "monthly-remittance" builds a per-day matrix instead.
        private RemittanceReport BuildRemittance(string type, List<PaymentRecord> payments, int? revenueTypeId, DateTime fromDate, DateTime toDate)
        {
            var report = new RemittanceReport { IsMonthly = type == "monthly-remittance" };

            // All active revenue types get a row (even with nothing collected);
            // a type filter narrows the list to that single type, active or not.
            var types = _context.RevenueTypes
                .AsNoTracking()
                .Include(t => t.Category)
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
                var groups = types
                    .GroupBy(t => t.CategoryID ?? "")
                    .OrderBy(g => g.First().Category?.Name ?? g.Key, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                foreach (var group in groups)
                {
                    var categoryName = group.First().Category?.Name ?? group.Key;
                    if (string.IsNullOrWhiteSpace(categoryName))
                        categoryName = "UNCATEGORIZED";

                    report.Rows.Add(new RemittanceRow
                    {
                        Name = $"{categoryName.ToUpperInvariant()}:",
                        IsSection = true
                    });

                    foreach (var t in group.OrderBy(t => t.TypeID))
                    {
                        report.Rows.Add(new RemittanceRow
                        {
                            Name = t.Name ?? "",
                            Total = byType.GetValueOrDefault(t.TypeID)
                        });
                    }
                }

                report.GrandTotal = report.Rows.Where(r => !r.IsSection).Sum(r => r.Total);
            }

            return report;
        }

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

        private static string ShortDayHeader(DateTime day)
        {
            var month = day.ToString("MMM", CultureInfo.InvariantCulture).ToUpperInvariant();
            if (month == "SEP") month = "SEPT";
            return $"{month}. {day.ToString("dd", CultureInfo.InvariantCulture)}";
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
            sb.AppendLine(CsvField("LGU TREASURER'S OFFICE COLLECTION RECORDING SYSTEM"));
            sb.AppendLine(CsvField($"Report Type: {reportType}"));
            if (!string.IsNullOrEmpty(revenueTypeName))
                sb.AppendLine(CsvField($"Revenue Type: {revenueTypeName}"));
            sb.AppendLine(CsvField($"Period: {from:MMMM dd, yyyy} to {to:MMMM dd, yyyy}"));
            if (report.IsMonthly)
                sb.AppendLine(CsvField("GENERAL COLLECTIONS"));
            sb.AppendLine(CsvField($"Generated: {DateTime.Now:MMMM dd, yyyy hh:mm tt}"));
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

            // UTF-8 with BOM so Excel shows characters correctly
            var bytes = new UTF8Encoding(true).GetPreamble()
                .Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
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
                    // Use a standard printable page so PDF viewers and printers do
                    // not crop the right-hand side of a wide custom page.
                    page.Size(PageSizes.A4.Landscape());
                }
                else
                {
                    page.Size(PageSizes.A4.Portrait());
                }
                page.Margin(20);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Lato"));

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
                        // Scale the layout down as the number of day columns grows
                        // so every column stays inside the printable page width.
                        var dayCount = report.Days.Count;
                        var cellFont = dayCount > 20 ? 4.5f : dayCount > 14 ? 5.5f : dayCount > 7 ? 6.5f : 7f;
                        var nameFont = Math.Min(7f, cellFont + 0.5f);
                        var cellPad = dayCount > 20 ? 1f : 2f;
                        var shortHeaders = dayCount > 10;

                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(2.6f);  // revenue type
                            foreach (var _ in report.Days)
                                cols.RelativeColumn(1f);
                            cols.RelativeColumn(1.3f);  // TOTAL
                        });

                        IContainer MCell(IContainer c, string? bg = null)
                        {
                            c = c.Border(1).BorderColor("#99AAAA").Padding(cellPad);
                            if (!string.IsNullOrEmpty(bg)) c = c.Background(bg);
                            return c;
                        }

                        IContainer MTot(IContainer c) =>
                            c.Border(1).BorderColor("#99AAAA").Background("#F0F2F6").Padding(cellPad);

                        table.Header(h =>
                        {
                            h.Cell().Element(c => MCell(c, "#F0F2F6")).Text("").FontSize(cellFont);
                            foreach (var day in report.Days)
                            {
                                h.Cell().Element(c => MCell(c, "#F0F2F6"))
                                    .Text(shortHeaders ? ShortDayHeader(day) : DayHeader(day))
                                    .Bold().FontSize(cellFont);
                            }
                            h.Cell().Element(c => MCell(c, "#F0F2F6"))
                                .Text("TOTAL").Bold().FontSize(cellFont);
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
                                .Text(row.Name).Bold().FontSize(nameFont);
                            if (isBt) nameText.FontColor("#FFFFFF");

                            for (var i = 0; i < report.Days.Count; i++)
                            {
                                table.Cell().Element(c => MCell(c, rowBg))
                                    .AlignRight().Text(AmountOrBlank(row.Cells[i])).FontSize(cellFont);
                            }

                            table.Cell().Element(c => MCell(c, rowBg)).AlignRight()
                                .Text(AmountOrBlank(row.Total))
                                .Bold().FontSize(cellFont).FontColor("#C0392B");
                        }

                        table.Cell().Element(MTot).Text("Total").Bold().FontSize(cellFont).FontColor("#C0392B");
                        foreach (var total in report.DayTotals)
                        {
                            table.Cell().Element(MTot).AlignRight().Text(AmountOrBlank(total))
                                .Bold().FontSize(cellFont).FontColor("#C0392B");
                        }
                        table.Cell().Element(MTot).AlignRight().Text(AmountOrBlank(report.GrandTotal))
                            .Bold().FontSize(cellFont).FontColor("#C0392B");
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