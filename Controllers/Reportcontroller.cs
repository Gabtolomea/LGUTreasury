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

            fromDate = fromDate.Date;
            toDate = toDate.Date.AddDays(1).AddSeconds(-1);

            // Payments (with line items) in the date range
            var query = _context.PaymentRecords
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
                "daily"   => "Daily Collection Report",
                "monthly" => "Monthly Revenue Summary",
                "annual"  => "Annual Revenue Summary",
                "custom"  => "Custom Report",
                _         => "Revenue Summary"
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
        // NOTE: change this one line if your RecordLineItem uses a different property name
        // (e.g. l.BaseAmount, or l.BaseAmount + l.Surcharge + l.Interest).
        private static decimal LineAmount(RecordLineItem l) => l.BaseAmount;

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
                    m.Rows.Add((t.TypeID, t.Name));
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