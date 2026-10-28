using LGUTreasury.Data;
using LGUTreasury.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LGUTreasury.Controllers
{
    public class RecordController : Controller
    {
        private readonly ApplicationDbContext _context;

        public RecordController(ApplicationDbContext context)
        {
            _context = context;
        }

      // Replace SearchPayee in RecordController.cs
[HttpGet]
public async Task<IActionResult> SearchPayee(string? query)
{
    // only payors that still have at least one non-deleted payment
    var payees = _context.Payees
        .Where(p => _context.PaymentRecords
            .Any(r => r.PayeeID == p.PayeeID && !r.IsDeleted));

    if (!string.IsNullOrWhiteSpace(query))
    {
        foreach (var word in query.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var w = word;
            payees = payees.Where(p =>
                p.Firstname!.Contains(w) ||
                p.Middlename!.Contains(w) ||
                p.Lastname!.Contains(w));
        }
    }

    var results = await payees
        .OrderBy(p => p.Lastname).ThenBy(p => p.Firstname)
        .Take(8)
        .Select(p => new {
            p.PayeeID, p.Firstname, p.Middlename, p.Lastname,
            p.Suffix, p.ContactNumber, p.ResidenceAddress
        })
        .ToListAsync();

    return Json(results.Select(r => new {
        r.PayeeID, r.Firstname, r.Middlename, r.Lastname,
        r.Suffix,
        ContactNumber = ContactNumberRules.Format(r.ContactNumber),
        r.ResidenceAddress
    }));
}

        [HttpPost]
        public async Task<IActionResult> SavePayee([FromBody] SavePayeeRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.FirstName) || string.IsNullOrWhiteSpace(req.LastName))
                return Json(new { success = false, message = "First and last name are required." });

            if (!ContactNumberRules.IsValid(req.ContactNumber))
                return Json(new { success = false, message = ContactNumberRules.LettersErrorMessage });

            var payee = new Payee
            {
                Firstname = req.FirstName, Middlename = req.MiddleName,
                Lastname = req.LastName, Suffix = req.Suffix,
                ContactNumber = ContactNumberRules.Normalize(req.ContactNumber),
                ResidenceAddress = req.ResidenceAddress,
                CreatedAt = DateTime.Now
            };
            _context.Payees.Add(payee);
            await _context.SaveChangesAsync();

            return Json(new {
                success = true,
                payee = new {
                    payeeID = payee.PayeeID, firstname = payee.Firstname,
                    middlename = payee.Middlename, lastname = payee.Lastname,
                    suffix = payee.Suffix,
                    contactNumber = ContactNumberRules.Format(payee.ContactNumber)
                }
            });
        }

    

        [HttpGet]
        public async Task<IActionResult> GetPaymentDetails(int paymentID)
        {
            var payment = await _context.PaymentRecords
                .Include(p => p.RecordLineItems).ThenInclude(l => l.RevenueType)
                .Include(p => p.CollectedBy)
                .FirstOrDefaultAsync(p => p.PaymentID == paymentID);

            if (payment == null) return Json(new { success = false });

            return Json(new {
                success            = true,
                officialReceipt    = payment.OfficialReceipt,
                dateIssued         = payment.DateIssued.ToString("yyyy-MM-dd"),
                paymentMethod      = payment.PaymentMethod,
                remarks            = payment.Remarks,
                totalAmount        = payment.TotalAmount,
                collectedBy_UserID = payment.CollectedBy_UserID,
                collectedByName    = payment.CollectedBy != null
                    ? $"{payment.CollectedBy.LastName}, {payment.CollectedBy.FirstName}" : "—",
                typeID   = payment.RecordLineItems?.FirstOrDefault()?.TypeID,
                typeName = payment.RecordLineItems?.FirstOrDefault()?.RevenueType?.Name
            });
        }

        public async Task<IActionResult> Create()
        {
            var userID = HttpContext.Session.GetInt32("UserID");
            if (userID == null) return RedirectToAction("Login", "Account");

            var role = HttpContext.Session.GetString("Role");
            if (role == "Collector") return RedirectToAction("Index");

            ViewData["ActiveNav"] = "record";
            ViewData["FullName"]  = HttpContext.Session.GetString("FullName");
            ViewData["Role"]      = role;
            ViewData["Initials"]  = GetInitials(ViewData["FullName"]?.ToString());

            await ReloadCreateViewBags();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(
            string? FullName, string OfficialReceipt, DateTime DateIssued,
            string? Remarks, string? PaymentMethod, int TypeID,
            decimal TotalBaseAmount, decimal TotalSurcharge,
            decimal TotalInterest, decimal TotalAmount, int CollectedBy_UserID)
        {
            var userID = HttpContext.Session.GetInt32("UserID");
            if (userID == null) return RedirectToAction("Login", "Account");

            var payorFullName = FullName?.Trim();

            if (DateIssued.Date > DateTime.Today)
            {
                TempData["Error"] = "Date issued cannot be in the future.";
                await ReloadCreateViewBags();
                return View();
            }

            if (string.IsNullOrWhiteSpace(payorFullName))
            {
                TempData["Error"] = "Please enter the payor's full name.";
                await ReloadCreateViewBags();
                return View();
            }

            if (TypeID == 0)
            {
                TempData["Error"] = "Please select a collection type.";
                await ReloadCreateViewBags();
                return View();
            }

            if (CollectedBy_UserID == 0)
            {
                TempData["Error"] = "Please select a collector.";
                await ReloadCreateViewBags();
                return View();
            }

            var payee = await FindOrCreatePayee(payorFullName);

            var payment = new PaymentRecord
            {
                OfficialReceipt = OfficialReceipt, PayeeID = payee.PayeeID,
                DateIssued = DateIssued, CollectedBy_UserID = CollectedBy_UserID,
                Remarks = Remarks, PaymentMethod = PaymentMethod,
                TotalBaseAmount = TotalBaseAmount, TotalSurcharge = TotalSurcharge,
                TotalInterest = TotalInterest, TotalAmount = TotalAmount,
                CreatedAt = DateTime.Now
            };
            _context.PaymentRecords.Add(payment);
            await _context.SaveChangesAsync();

            _context.RecordLineItems.Add(new RecordLineItem
            {
                PaymentID = payment.PaymentID, TypeID = TypeID, Quantity = 1,
                BaseAmount = TotalBaseAmount, SurchargeAmount = TotalSurcharge,
                InterestAmount = TotalInterest, LineTotal = TotalAmount
            });
            await _context.SaveChangesAsync();

            TempData["Toast"] = "Payment recorded successfully!";
            return RedirectToAction("Index");
        }

        [HttpPost]
public async Task<IActionResult> CreateBulk(
    int? PayeeID, string FullName, string OfficialReceipt,
    DateTime DateIssued, string? Remarks, string? PaymentMethod,
    int TypeID, decimal TotalBaseAmount, decimal TotalSurcharge,
    decimal TotalInterest, int CollectedBy_UserID)
{
    var userID = HttpContext.Session.GetInt32("UserID");
    if (userID == null) return RedirectToAction("Login", "Account");
    if (HttpContext.Session.GetString("Role") == "Collector") return RedirectToAction("Index");

    var m = System.Text.RegularExpressions.Regex.Match(
        OfficialReceipt ?? "", @"^\s*(\d+)\s*(?:-|–|to)\s*(\d+)\s*$",
        System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    string? error = null;
    long start = 0, end = 0;
    if (!m.Success) error = "Invalid OR range. Use a format like 60-578.";
    else
    {
        start = long.Parse(m.Groups[1].Value);
        end   = long.Parse(m.Groups[2].Value);
        if (end < start) error = "OR range end must not be lower than the start.";
    }
    if (error == null && DateIssued.Date > DateTime.Today)       error = "Date issued cannot be in the future.";
    if (error == null && string.IsNullOrWhiteSpace(FullName) && !(PayeeID > 0))
                                                                  error = "Please enter a label (e.g. Water).";
    if (error == null && TypeID == 0)                             error = "Please select a collection type.";
    if (error == null && CollectedBy_UserID == 0)                 error = "Please select a collector.";
    if (error == null && TotalBaseAmount + TotalSurcharge + TotalInterest <= 0)
                                                                  error = "Total amount must be greater than zero.";

    // Reject if any OR number in the range is already used (single OR or another range)
    if (error == null)
    {
        var used = await _context.PaymentRecords
            .Where(p => !p.IsDeleted && p.OfficialReceipt != null)
            .Select(p => p.OfficialReceipt!)
            .ToListAsync();

        foreach (var o in used)
        {
            var em = System.Text.RegularExpressions.Regex.Match(o, @"^\s*(\d+)(?:\s*-\s*(\d+))?\s*$");
            if (!em.Success) continue;
            var es = long.Parse(em.Groups[1].Value);
            var ee = em.Groups[2].Success ? long.Parse(em.Groups[2].Value) : es;
            if (es <= end && ee >= start)
            {
                error = $"OR number(s) already used by record {o}.";
                break;
            }
        }
    }

    if (error != null)
    {
        TempData["Error"] = error;
        await ReloadCreateViewBags();
        return View("Create");
    }

    int payeeId;
    if (PayeeID.HasValue && PayeeID.Value > 0)
    {
        payeeId = PayeeID.Value;
    }
    else
    {
        var label = FullName.Trim();
        var payee = await _context.Payees
            .FirstOrDefaultAsync(p => p.Firstname == label && (p.Lastname == "" || p.Lastname == null));
        if (payee == null)
        {
            payee = new Payee { Firstname = label, Lastname = "", CreatedAt = DateTime.Now };
            _context.Payees.Add(payee);
            await _context.SaveChangesAsync();
        }
        payeeId = payee.PayeeID;
    }

    int count = (int)(end - start + 1);
    var total = TotalBaseAmount + TotalSurcharge + TotalInterest;
    var orText = $"{m.Groups[1].Value}-{m.Groups[2].Value}";

    var payment = new PaymentRecord
    {
        OfficialReceipt = orText, PayeeID = payeeId,
        DateIssued = DateIssued, CollectedBy_UserID = CollectedBy_UserID,
        Remarks = Remarks, PaymentMethod = PaymentMethod,
        TotalBaseAmount = TotalBaseAmount, TotalSurcharge = TotalSurcharge,
        TotalInterest = TotalInterest, TotalAmount = total,
        CreatedAt = DateTime.Now,
        RecordLineItems = new List<RecordLineItem>
        {
            new RecordLineItem {
                TypeID = TypeID, Quantity = count,
                BaseAmount = TotalBaseAmount, SurchargeAmount = TotalSurcharge,
                InterestAmount = TotalInterest, LineTotal = total
            }
        }
    };
    _context.PaymentRecords.Add(payment);
    await _context.SaveChangesAsync();

    TempData["Toast"] = $"Recorded OR {orText} ({count} receipts), total ₱{total:N2}.";
    return RedirectToAction("Index");
}

        public async Task<IActionResult> Index()
        {
            var userID = HttpContext.Session.GetInt32("UserID");
            if (userID == null) return RedirectToAction("Login", "Account");

            var role = HttpContext.Session.GetString("Role");
            ViewData["ActiveNav"] = "records";
            ViewData["FullName"]  = HttpContext.Session.GetString("FullName");
            ViewData["Role"]      = role;
            ViewData["Initials"]  = GetInitials(ViewData["FullName"]?.ToString());

            var fromStr      = Request.Query["from"].ToString();
            var toStr        = Request.Query["to"].ToString();
            var collectorStr = Request.Query["collectorId"].ToString();

            var filterFrom = DateTime.TryParse(fromStr, out var pFrom) ? pFrom.Date : (DateTime?)null;
            var filterTo   = DateTime.TryParse(toStr,   out var pTo)   ? pTo.Date   : (DateTime?)null;
            var filterCollectorId = int.TryParse(collectorStr, out var pCol) && pCol > 0 ? pCol : (int?)null;

            ViewBag.FilterFrom        = filterFrom?.ToString("yyyy-MM-dd") ?? "";
            ViewBag.FilterTo          = filterTo?.ToString("yyyy-MM-dd") ?? "";
            ViewBag.FilterCollectorId = filterCollectorId ?? 0;

            var query = _context.PaymentRecords
                .Include(p => p.Payee)
                .Include(p => p.CollectedBy)
                .Include(p => p.RecordLineItems).ThenInclude(l => l.RevenueType)
                .Where(p => !p.IsDeleted)
                .AsQueryable();

            if (filterFrom.HasValue)
                query = query.Where(p => p.DateIssued.Date >= filterFrom.Value);
            if (filterTo.HasValue)
                query = query.Where(p => p.DateIssued.Date <= filterTo.Value);
            if (filterCollectorId.HasValue)
                query = query.Where(p => p.CollectedBy_UserID == filterCollectorId.Value);

            var records = await query.OrderByDescending(p => p.DateIssued).ToListAsync();

            ViewBag.FilteredTotal = records.Sum(p => p.TotalAmount).ToString("N2");
            ViewBag.FilteredCount = records.Count;

            if (role == "Officer" || role == "Admin")
            {
                ViewBag.EditRequests = await _context.Editrequests
                    .Include(r => r.PaymentRecord).ThenInclude(p => p!.Payee)
                    .Include(r => r.RequestedBy)
                    .OrderByDescending(r => r.CreatedAt)
                    .ToListAsync();

                ViewBag.DeletedRecords = await _context.DeletedRecords
                    .Include(d => d.DeletedBy)
                    .OrderByDescending(d => d.DeletedAt)
                    .ToListAsync();
            }

            ViewBag.RevenueTypes = await _context.RevenueTypes
                .Where(r => r.IsActive)
                .Select(r => new { r.TypeID, r.Name })
                .ToListAsync();

            ViewBag.Collectors = await _context.UserAccounts
                .Where(u => u.Role == "Collector" && u.IsActive == true)
                .OrderBy(u => u.LastName)
                .ToListAsync();

            return View(records);
        }
        

        [HttpPost]
        public async Task<IActionResult> EditRecord(
            int PaymentID, string OfficialReceipt, DateTime DateIssued,
            string? PaymentMethod, string? Remarks, decimal TotalAmount,
            int CollectedBy_UserID)
        {
            var userID = HttpContext.Session.GetInt32("UserID");
            if (userID == null) return Json(new { success = false, message = "Not logged in." });

            var role = HttpContext.Session.GetString("Role");
            if (role == "Collector") return Json(new { success = false, message = "Unauthorized." });

            var payment = await _context.PaymentRecords
                .Include(p => p.RecordLineItems)
                .FirstOrDefaultAsync(p => p.PaymentID == PaymentID);

            if (payment == null) return Json(new { success = false, message = "Record not found." });

            payment.OfficialReceipt    = OfficialReceipt;
            payment.DateIssued         = DateIssued;
            payment.PaymentMethod      = PaymentMethod;
            payment.Remarks            = Remarks;
            payment.TotalAmount        = TotalAmount;
            payment.TotalBaseAmount    = TotalAmount;
            payment.CollectedBy_UserID = CollectedBy_UserID;

            var lineItem = payment.RecordLineItems?.FirstOrDefault();
            if (lineItem != null)
            {
                lineItem.LineTotal       = TotalAmount;
                lineItem.BaseAmount      = TotalAmount;
                lineItem.SurchargeAmount = 0;
                lineItem.InterestAmount  = 0;
            }

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        // POST: /Record/DeleteRecord
        [HttpPost]
        public async Task<IActionResult> DeleteRecord(int PaymentID)
        {
            var userID = HttpContext.Session.GetInt32("UserID");
            if (userID == null) return Json(new { success = false, message = "Not logged in." });

            var role = HttpContext.Session.GetString("Role");
            if (role != "Officer" && role != "Admin")
                return Json(new { success = false, message = "Unauthorized." });

            var payment = await _context.PaymentRecords
                .Include(p => p.Payee)
                .Include(p => p.CollectedBy)
                .Include(p => p.RecordLineItems).ThenInclude(l => l.RevenueType)
                .FirstOrDefaultAsync(p => p.PaymentID == PaymentID);

            if (payment == null) return Json(new { success = false, message = "Record not found." });

            var user = await _context.UserAccounts.FindAsync(userID);

            var deleted = new DeletedRecord
            {
                PaymentID        = payment.PaymentID,
                PayeeName        = payment.Payee != null ? $"{payment.Payee.Lastname}, {payment.Payee.Firstname}" : "—",
                CollectorName    = payment.CollectedBy != null ? $"{payment.CollectedBy.LastName}, {payment.CollectedBy.FirstName}" : "—",
                CollectionType   = payment.RecordLineItems?.FirstOrDefault()?.RevenueType?.Name ?? "—",
                DeletedBy_UserID = userID.Value,
                DeletedByName    = user != null ? $"{user.LastName}, {user.FirstName}" : "—",
                DeletedAt        = DateTime.Now
            };

            _context.DeletedRecords.Add(deleted);
            payment.IsDeleted = true;
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        // POST: /Record/RestoreRecord
        [HttpPost]
        public async Task<IActionResult> RestoreRecord(int DeletedRecordID)
        {
            var userID = HttpContext.Session.GetInt32("UserID");
            if (userID == null) return Json(new { success = false, message = "Not logged in." });

            var role = HttpContext.Session.GetString("Role");
            if (role != "Officer" && role != "Admin")
                return Json(new { success = false, message = "Unauthorized." });

            var deletedRecord = await _context.DeletedRecords
                .FirstOrDefaultAsync(d => d.DeletedRecordID == DeletedRecordID);

            if (deletedRecord == null) return Json(new { success = false, message = "Record not found." });

            var payment = await _context.PaymentRecords
                .FirstOrDefaultAsync(p => p.PaymentID == deletedRecord.PaymentID);

            if (payment == null) return Json(new { success = false, message = "Original record not found." });

            payment.IsDeleted = false;
            _context.DeletedRecords.Remove(deletedRecord);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        // POST: /Record/PermanentDelete
        [HttpPost]
        public async Task<IActionResult> PermanentDelete(int DeletedRecordID)
        {
            var userID = HttpContext.Session.GetInt32("UserID");
            if (userID == null) return Json(new { success = false, message = "Not logged in." });

            var role = HttpContext.Session.GetString("Role");
            if (role != "Officer" && role != "Admin")
                return Json(new { success = false, message = "Unauthorized." });

            var deletedRecord = await _context.DeletedRecords
                .FirstOrDefaultAsync(d => d.DeletedRecordID == DeletedRecordID);

            if (deletedRecord == null) return Json(new { success = false, message = "Record not found." });

            var payment = await _context.PaymentRecords
                .Include(p => p.RecordLineItems)
                .FirstOrDefaultAsync(p => p.PaymentID == deletedRecord.PaymentID);

            if (payment != null)
            {
                if (payment.RecordLineItems != null)
                    _context.RecordLineItems.RemoveRange(payment.RecordLineItems);
                _context.PaymentRecords.Remove(payment);
            }

            _context.DeletedRecords.Remove(deletedRecord);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> SendMessage(int PaymentID, string? Reason)
        {
            var userID = HttpContext.Session.GetInt32("UserID");
            if (userID == null) return Json(new { success = false, message = "Not logged in." });

            if (string.IsNullOrWhiteSpace(Reason))
                return Json(new { success = false, message = "Please enter a message." });

            var payment = await _context.PaymentRecords.FindAsync(PaymentID);
            if (payment == null) return Json(new { success = false, message = "Payment not found." });

            if (payment.HasPendingRequest)
                return Json(new { success = false, message = "This record already has a pending message." });

            var request = new EditRequest
            {
                PaymentID          = PaymentID,
                RequestedBy_UserID = userID.Value,
                Reason             = Reason,
                Status             = "Pending",
                CreatedAt          = DateTime.Now
            };
            _context.Editrequests.Add(request);
            payment.HasPendingRequest = true;
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> ResolveMessage(int RequestID, string? ReviewNote)
        {
            var userID = HttpContext.Session.GetInt32("UserID");
            if (userID == null) return Json(new { success = false, message = "Not logged in." });

            var request = await _context.Editrequests
                .Include(r => r.PaymentRecord)
                .FirstOrDefaultAsync(r => r.RequestID == RequestID);

            if (request == null) return Json(new { success = false, message = "Message not found." });

            request.Status            = "Resolved";
            request.ReviewedBy_UserID = userID.Value;
            request.ReviewNote        = ReviewNote;
            request.ReviewedAt        = DateTime.Now;

            if (request.PaymentRecord != null)
                request.PaymentRecord.HasPendingRequest = false;

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        // The record payment page captures a single full name instead of selecting a
        // payor, so split it into first/last and reuse an existing matching payor when
        // one already exists.
        private async Task<Payee> FindOrCreatePayee(string fullName)
{
    string firstName, lastName;
    var name = fullName.Trim();
    if (name.Contains(','))
    {
        // "Chua, Janice Angel" -> last = Chua, first = Janice Angel
        var halves = name.Split(',', 2);
        lastName  = halves[0].Trim();
        firstName = halves[1].Trim();
    }
    else
    {
        var parts = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length <= 1) { firstName = name; lastName = ""; }
        else { lastName = parts[^1]; firstName = string.Join(' ', parts[..^1]); }
    }

    var existing = await _context.Payees
        .FirstOrDefaultAsync(p => p.Firstname == firstName && p.Lastname == lastName);

            var payee = new Payee
            {
                Firstname = firstName, Lastname = lastName, CreatedAt = DateTime.Now
            };
            _context.Payees.Add(payee);
            await _context.SaveChangesAsync();
            return payee;
        }

        private async Task ReloadCreateViewBags()
        {
    ViewData["ActiveNav"] = "record";
    ViewData["FullName"]  = HttpContext.Session.GetString("FullName");
    ViewData["Role"]      = HttpContext.Session.GetString("Role");
    ViewData["Initials"]  = GetInitials(ViewData["FullName"]?.ToString());

            ViewBag.RevenueTypes = await _context.RevenueTypes
                .Include(r => r.RevenuePolicies)
                .Where(r => r.IsActive)
                .Select(r => new {
                    r.TypeID, r.Name, r.BaseRate,
                    SurchargeRate = r.RevenuePolicies.FirstOrDefault() != null ? r.RevenuePolicies.FirstOrDefault()!.SurchargeRate : 0,
                    InterestRate  = r.RevenuePolicies.FirstOrDefault() != null ? r.RevenuePolicies.FirstOrDefault()!.InterestRate  : 0
                })
                .ToListAsync();

            ViewBag.Collectors = await _context.UserAccounts
                .Where(u => u.Role == "Collector" && u.IsActive == true)
                .ToListAsync();
        }

        private string GetInitials(string? fullName)
        {
            if (string.IsNullOrEmpty(fullName)) return "?";
            var parts = fullName.Split(' ');
            if (parts.Length >= 2) return $"{parts[0][0]}{parts[^1][0]}".ToUpper();
            return fullName[0].ToString().ToUpper();
        }
    }

    public class SavePayeeRequest
    {
        public string FirstName         { get; set; } = "";
        public string? MiddleName       { get; set; }
        public string LastName          { get; set; } = "";
        public string? Suffix           { get; set; }
        [ContactNumber]
        public string? ContactNumber    { get; set; }
        public string? ResidenceAddress { get; set; }
    }
}
