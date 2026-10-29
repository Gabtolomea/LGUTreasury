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

            var fullName = HttpContext.Session.GetString("FullName") ?? "Officer";

            // Save the parameters so the report can be downloaded again later
            var log = new ReportLog
            {
                ReportType = ReportTypeName(type),
                ReportKind = type,
                Format = normalizedFormat.ToUpper(),
                GeneratedAt = DateTime.Now,
                GeneratedByUserID = userID.Value,
                GeneratedByName = fullName,
                FromDate = fromDate.Date,
                ToDate = toDate.Date,
                RevenueTypeID = revenueTypeId
            };
            _context.ReportLog.Add(log);
            await _context.SaveChangesAsync();

            return await BuildReportFile(type, normalizedFormat, fromDate.Date, toDate.Date, revenueTypeId, fullName);
        }

        // GET: /Report/Download/5
        // Re-creates a previously generated report from its saved parameters.
        // Does NOT add a new row to the log.
        public async Task<IActionResult> Download(int id)
        {
            if (HttpContext.Session.GetInt32("UserID") == null)
                return RedirectToAction("Login", "Account");

            var log = await _context.ReportLog.FindAsync(id);
            if (log == null)
                return NotFound("Report not found.");

            if (log.FromDate == null || log.ToDate == null)
                return BadRequest("This report was generated before downloads were supported. Please generate it again.");

            var fullName = log.GeneratedByName
                           ?? HttpContext.Session.GetString("FullName")
                           ?? "Officer";

            return await BuildReportFile(
                log.ReportKind ?? "custom",
                (log.Format ?? "pdf").ToLowerInvariant(),
                log.FromDate.Value.Date,
                log.ToDate.Value.Date,
                log.RevenueTypeID,
                fullName);
        }

        private static string ReportTypeName(string? type) => type switch
        {
            "daily"   => "Daily Collection Report",
            "monthly" => "Monthly Revenue Summary",
            "annual"  => "Annual Revenue Summary",
            "custom"  => "Custom Report",
            _         => "Revenue Summary"
        };

        // Builds the file for a date range. Used by both Generate and Download.
        private async Task<IActionResult> BuildReportFile(
            string type, string format, DateTime fromDate, DateTime toDate,
            int? revenueTypeId, string fullName)
        {
            fromDate = fromDate.Date;
            toDate = toDate.Date.AddDays(1).AddSeconds(-1);   // end of the "to" day

            var query = _context.PaymentRecords
                .Include(p => p.RecordLineItems)
                    .ThenInclude(l => l.RevenueType)
                .Where(p => p.DateIssued >= fromDate && p.DateIssued <= toDate);

            if (revenueTypeId.HasValue)
                query = query.Where(p => p.RecordLineItems.Any(l => l.TypeID == revenueTypeId.Value));

            var payments = await query
                .OrderBy(p => p.DateIssued)
                .ToListAsync();

            // Rows come from the RevenueTypes table, in the sheet's order
            var revenueTypes = await _context.RevenueTypes
                .OrderBy(r => r.Name)
                .ToListAsync();

            var matrix = BuildMatrix(payments, revenueTypes, fromDate, toDate, revenueTypeId);

            var reportTypeName = ReportTypeName(type);

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

            return format switch
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
            public List<(int TypeID, string Name, string? Color, bool LabelOnly)> Rows { get; } = new();
            public Dictionary<(int TypeID, DateTime Bucket), decimal> Cells { get; } = new();

            public decimal Cell(int typeId, DateTime bucket) =>
                Cells.TryGetValue((typeId, bucket), out var v) ? v : 0m;

            public decimal RowTotal(int typeId) => Columns.Sum(c => Cell(typeId, c.Start));
            public decimal ColTotal(DateTime bucket) => Rows.Sum(r => Cell(r.TypeID, bucket));
            public decimal GrandTotal => Rows.Sum(r => RowTotal(r.TypeID));
        }

        // >>> KEEP YOUR OWN VERSION OF THIS LINE (the one that already compiles for you). <<<
        // It is whatever property on RecordLineItem holds the amount.
        private static decimal LineAmount(RecordLineItem l) => l.BaseAmount;

        // lower-case letters/digits only: "Mayor's Permit Fees" and "mayors permit fees" match
        private static string Norm(string? s) =>
            new string((s ?? "").Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();

        // Older / shorter names -> the name used on the sheet (keys are Norm()-ed)
        private static readonly Dictionary<string, string> NameAliases = new()
        {
            ["mayorpermit"]      = "Mayor's Permit",
            ["mayorspermit"]     = "Mayor's Permit",
            ["mayorpermits"]     = "Mayor's Permit",
            ["mayorspermitfees"] = "Mayor's Permit",
            ["businesstax"]      = "Mun. License & Business Tax",
            ["burialpermit"]     = "Burial",
            ["kabutungan"]       = "KABUTONGAN",
            ["buildingpermit"]   = "Building Permit Fee",
            ["buildingpermits"]  = "Building Permit Fee",
            ["civilregistry"]    = "Civil Registry Fees",
            ["occupationalfee"]  = "Occupational Fees",
            ["watersfees"]       = "Water Fees",
            ["waterfee"]         = "Water Fees",
            ["marketrentals"]    = "Market/Rentals",
            ["marketrental"]     = "Market/Rentals",
            ["garbagefees"]      = "Garbage Fee",
            ["cockfighting"]     = "Cock Fight",
        };

        // The paper sheet's row ORDER and band COLORS (fixed, so they live here, not in the database).
        // Rows are matched to the RevenueTypes table by Name. Types not listed go to the bottom.
        // Label   = text printed on the report
        // Color   = band color (whole row for Kabutongan/Inambakan/Cock Fight, label cell only for the Business Taxes block)
        // Match   = RevenueTypes.Name to take the numbers from (defaults to Label)
        // Heading = title row with no numbers      Indent = shown indented under the heading
        private sealed record SheetRow(
            string Label, string? Color = null, string? Match = null,
            bool Heading = false, bool Indent = false);

        private static readonly SheetRow[] SheetLayout =
        {
            new("Mun. License & Business Tax"),
            new("Mayor's Permit"),
            new("Garbage Fee"),
            new("BUSINESS TAXES", "#5B9BD5", Heading: true),
            new("Retailers", "#9BC2E6", "Business Tax - Retailers", Indent: true),
            new("Financial", "#A9D08E", "Business Tax - Financial", Indent: true),
            new("Others",    "#F4B183", "Business Tax - Others",    Indent: true),
            new("Peddlers Tax"),
            new("CTC"),
            new("Police Clearance"),
            new("A.I. Fee"),
            new("ZONING"),
            new("Building Permit Fee"),
            new("Brgy./Sec.'s Fee"),
            new("Miscellaneous"),
            new("Medical, Dental and Lab. Fee"),
            new("Fishery Rentals, Fees and Charges/Service Income"),
            new("CIVIL REGISTRY FEES"),
            new("Other Clearances and Cert."),
            new("POLICE"),
            new("BUSINESS INCOME/PARKING FEE"),
            new("OCCUPATIONAL FEES"),
            new("Marriage"),
            new("Birth Certificate"),
            new("Cab/Bicycle Registration"),
            new("Burial"),
            new("Weights and Measure"),
            new("Water Fees"),
            new("Market/Cash Tickets"),
            new("Market/Rentals"),
            new("BIR Doc Stamp"),
            new("PTR"),
            new("Mining Fee"),
            new("KABUTONGAN", "#92D050"),   // green band
            new("Inambakan",  "#00B0F0"),   // blue band
            new("COCK FIGHT", "#FFFF00"),   // yellow band
            new("Brgy. Clearance"),
            new("PCA"),
        };

        // Month names the way the paper sheet writes them
        private static readonly string[] MonthAbbr =
            { "JAN.", "FEB.", "MAR.", "APR.", "MAY", "JUNE", "JULY", "AUG.", "SEPT.", "OCT.", "NOV.", "DEC." };

        private static string DayLabel(DateTime d) => $"{MonthAbbr[d.Month - 1]} {d.Day:00}, {d.Year}";
        private static string MonthLabel(DateTime d) => $"{MonthAbbr[d.Month - 1]} {d.Year}";

        // Top-left cell of the sheet ("Sep-26")
        private static string PeriodLabel(DateTime from, DateTime to)
        {
            var f = from.ToString("MMM-yy", CultureInfo.InvariantCulture);
            var t = to.ToString("MMM-yy", CultureInfo.InvariantCulture);
            return f == t ? f : $"{f} to {t}";
        }

        private static Matrix BuildMatrix(
            List<PaymentRecord> payments, List<RevenueType> types,
            DateTime from, DateTime to, int? revenueTypeId)
        {
            // Up to ~31 days: one column per day (like the paper sheet).
            // Longer ranges: one column per month.
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
                    m.Columns.Add((d, MonthLabel(d)));
            }
            else
            {
                // Every weekday in the range, plus any weekend date that has collections
                var dataDates = m.Cells.Keys.Select(k => k.Bucket).ToHashSet();
                for (var d = from.Date; d <= to.Date; d = d.AddDays(1))
                {
                    var weekend = d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday;
                    if (!weekend || dataDates.Contains(d))
                        m.Columns.Add((d, DayLabel(d)));
                }
            }

            // Rows: every active type (even with no collections, like the sheet);
            // inactive types only if they have collections in the range.
            // 1) EVERY line of the paper sheet, in the sheet's order, always shown
            //    (even if the type is inactive, or not in the table yet -> blank row).
            // Names are matched ignoring case, spaces and punctuation, and a few older/shorter
            // names are mapped to the sheet's name (see NameAliases).
            var byName = new Dictionary<string, RevenueType>();
            foreach (var t in types)
            {
                var key = Norm(t.Name);
                if (NameAliases.TryGetValue(key, out var canonical)) key = Norm(canonical);
                if (!byName.ContainsKey(key)) byName[key] = t;
            }

            var used = new HashSet<int>();
            var missingId = -1;   // placeholder id for sheet lines that have no row in the table
            foreach (var row in SheetLayout)
            {
                var label = (row.Indent ? "\u00A0\u00A0\u00A0\u00A0" : "") + row.Label;
                int id;
                if (row.Heading)
                    id = missingId--;   // title row, never has numbers
                else
                    id = byName.TryGetValue(Norm(row.Match ?? row.Label), out var found) ? found.TypeID : missingId--;

                if (revenueTypeId.HasValue && id != revenueTypeId.Value) continue;
                used.Add(id);
                m.Rows.Add((id, label, row.Color, row.Heading || row.Indent));
            }

            // 2) Any other types in the table that are not on the sheet:
            //    shown at the bottom if active or if they have collections in the range.
            foreach (var t in types.OrderBy(t => t.Name))
            {
                if (used.Contains(t.TypeID)) continue;
                if (revenueTypeId.HasValue && t.TypeID != revenueTypeId.Value) continue;
                var hasData = m.Cells.Keys.Any(k => k.TypeID == t.TypeID);
                if (t.IsActive || hasData)
                    m.Rows.Add((t.TypeID, (t.Name ?? "").Trim(), null, false));
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

            sb.AppendLine(string.Join(",",
                new[] { CsvField(PeriodLabel(from, to)) }
                    .Concat(m.Columns.Select(c => CsvField(c.Label)))
                    .Append(CsvField("TOTAL"))));

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

            var totals = new List<string> { CsvField("Total") };
            totals.AddRange(m.Columns.Select(c => m.ColTotal(c.Start).ToString("0.00", CultureInfo.InvariantCulture)));
            totals.Add(m.GrandTotal.ToString("0.00", CultureInfo.InvariantCulture));
            sb.AppendLine(string.Join(",", totals));

            var bytes = new UTF8Encoding(true).GetPreamble()
                .Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
            return File(bytes, "text/csv", fileName + ".csv");
        }

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

            var n = m.Columns.Count;
            var totalCols = n + 2;   // name + dates + TOTAL
            const string red = "#C62828";

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

            var h1 = 5;
            if (!string.IsNullOrEmpty(revenueTypeName))
            {
                Title(4, $"Revenue Type: {revenueTypeName}", false, 10);
                h1 = 6;
            }
            var h2 = h1 + 1;

            // Header, 2 rows like the sheet:  [Sep-26 | GENERAL COLLECTIONS | TOTAL]
            //                                 [       | SEPT. 01, 2026 ...  |       ]
            ws.Cell(h1, 1).Value = PeriodLabel(from, to);
            if (n > 0)
            {
                ws.Cell(h1, 2).Value = "GENERAL COLLECTIONS";
                ws.Range(h1, 2, h1, n + 1).Merge();
            }
            for (var i = 0; i < n; i++)
                ws.Cell(h2, i + 2).Value = m.Columns[i].Label;
            ws.Cell(h1, totalCols).Value = "TOTAL";
            ws.Range(h1, totalCols, h2, totalCols).Merge();

            var header = ws.Range(h1, 1, h2, totalCols);
            header.Style.Font.Bold = true;
            header.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            header.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            header.Style.Alignment.WrapText = true;
            header.Style.Fill.BackgroundColor = XLColor.FromHtml("#F2F2F2");

            // Body: one row per revenue type
            var row = h2 + 1;
            foreach (var r in m.Rows)
            {
                if (!string.IsNullOrEmpty(r.Color))
                    (r.LabelOnly ? ws.Range(row, 1, row, 1) : ws.Range(row, 1, row, totalCols))
                        .Style.Fill.BackgroundColor = XLColor.FromHtml(r.Color);

                ws.Cell(row, 1).Value = r.Name;
                ws.Cell(row, 1).Style.Font.Bold = true;

                for (var i = 0; i < n; i++)
                {
                    var v = m.Cell(r.TypeID, m.Columns[i].Start);
                    if (v != 0) ws.Cell(row, i + 2).Value = (double)v;
                }

                var rt = m.RowTotal(r.TypeID);
                if (rt != 0) ws.Cell(row, totalCols).Value = (double)rt;
                ws.Cell(row, totalCols).Style.Font.FontColor = XLColor.FromHtml(red);
                row++;
            }

            // Total row (red, like the sheet)
            ws.Cell(row, 1).Value = "Total";
            for (var i = 0; i < n; i++)
                ws.Cell(row, i + 2).Value = (double)m.ColTotal(m.Columns[i].Start);
            ws.Cell(row, totalCols).Value = (double)m.GrandTotal;
            ws.Range(row, 1, row, totalCols).Style.Font.FontColor = XLColor.FromHtml(red);
            ws.Range(row, 1, row, totalCols).Style.Font.Bold = true;

            // Grid + formatting
            var table = ws.Range(h1, 1, row, totalCols);
            table.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
            table.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

            var nums = ws.Range(h2 + 1, 2, row, totalCols);
            nums.Style.NumberFormat.Format = "#,##0.00";
            nums.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            ws.Column(1).Width = 40;
            for (var c = 2; c <= totalCols; c++) ws.Column(c).Width = 14;

            ws.SheetView.Freeze(h2, 1);
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
            var n = m.Columns.Count;
            var wide = n > 12;
            var fontSize = n > 24 ? 6f : 7f;
            const string red = "#C62828";

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
                        col.Item().PaddingTop(4).BorderBottom(1).BorderColor("#000000");
                    });

                    page.Content().PaddingTop(10).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.ConstantColumn(wide ? 150 : 120);               // revenue type
                            foreach (var _ in m.Columns) cols.RelativeColumn();  // one per date
                            cols.ConstantColumn(wide ? 70 : 62);                 // TOTAL
                        });

                        IContainer Box(IContainer c, string? bg) =>
                            c.Border(0.5f).BorderColor("#616161").Background(bg ?? "#FFFFFF").Padding(3);

                        // Header (2 rows): [Sep-26 | GENERAL COLLECTIONS | TOTAL]
                        //                  [       | dates...            |       ]
                        table.Header(h =>
                        {
                            h.Cell().Element(c => Box(c, "#F2F2F2").AlignCenter())
                                .Text(PeriodLabel(from, to)).Bold().FontSize(fontSize);
                            if (n > 0)
                                h.Cell().ColumnSpan((uint)n).Element(c => Box(c, "#F2F2F2").AlignCenter())
                                    .Text("GENERAL COLLECTIONS").Bold().FontSize(fontSize);
                            h.Cell().RowSpan(2).Element(c => Box(c, "#F2F2F2").AlignCenter().AlignMiddle())
                                .Text("TOTAL").Bold().FontSize(fontSize).FontColor(red);

                            h.Cell().Element(c => Box(c, "#F2F2F2")).Text("");
                            foreach (var col in m.Columns)
                                h.Cell().Element(c => Box(c, "#F2F2F2").AlignCenter())
                                    .Text(col.Label).Bold().FontSize(fontSize);
                        });

                        foreach (var r in m.Rows)
                        {
                            var bg = string.IsNullOrEmpty(r.Color) ? null : r.Color;
                            var bgRow = r.LabelOnly ? null : bg;   // label-only for the Business Taxes block

                            table.Cell().Element(c => Box(c, bg))
                                .Text(r.Name).Bold().FontSize(fontSize);
                            foreach (var col in m.Columns)
                                table.Cell().Element(c => Box(c, bgRow)).AlignRight()
                                    .Text(Amt(m.Cell(r.TypeID, col.Start))).FontSize(fontSize);
                            table.Cell().Element(c => Box(c, bgRow)).AlignRight()
                                .Text(Amt(m.RowTotal(r.TypeID))).FontSize(fontSize).FontColor(red);
                        }

                        // Total row (red, like the sheet)
                        table.Cell().Element(c => Box(c, null))
                            .Text("Total").Bold().FontSize(fontSize).FontColor(red);
                        foreach (var col in m.Columns)
                            table.Cell().Element(c => Box(c, null)).AlignRight()
                                .Text(m.ColTotal(col.Start).ToString("N2", CultureInfo.InvariantCulture))
                                .Bold().FontSize(fontSize).FontColor(red);
                        table.Cell().Element(c => Box(c, null)).AlignRight()
                            .Text(m.GrandTotal.ToString("N2", CultureInfo.InvariantCulture))
                            .Bold().FontSize(fontSize).FontColor(red);
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