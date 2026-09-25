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

        [HttpGet]
        public async Task<IActionResult> SearchPayee(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Json(new List<object>());

            var results = await _context.Payees
                .Where(p => p.Firstname!.Contains(query) || p.Lastname!.Contains(query))
                .Take(8)
                .Select(p => new {
                    p.PayeeID, p.Firstname, p.Middlename, p.Lastname,
                    p.Suffix, p.ContactNumber, p.ResidenceAddress
                })
                .ToListAsync();

            return Json(results);
        }

        [HttpPost]
        public async Task<IActionResult> SavePayee([FromBody] SavePayeeRequest req)
        {
            if (string.IsNullOrWhiteSpace(req.FirstName) || string.IsNullOrWhiteSpace(req.LastName))
                return Json(new { success = false, message = "First and last name are required." });

            var payee = new Payee
            {
                Firstname = req.FirstName, Middlename = req.MiddleName,
                Lastname = req.LastName, Suffix = req.Suffix,
                ContactNumber = req.ContactNumber, ResidenceAddress = req.ResidenceAddress,
                CreatedAt = DateTime.Now
            };
            _context.Payees.Add(payee);
            await _context.SaveChangesAsync();

            return Json(new {
                success = true,
                payee = new {
                    payeeID = payee.PayeeID, firstname = payee.Firstname,
                    middlename = payee.Middlename, lastname = payee.Lastname,
                    suffix = payee.Suffix, contactNumber = payee.ContactNumber,
                    residenceAddress = payee.ResidenceAddress
                }
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetNextTransactionID()
        {
            var lastID = await _context.PaymentRecords
                .OrderByDescending(p => p.PaymentID)
                .Select(p => p.PaymentID)
                .FirstOrDefaultAsync();
            return Json(new { transactionID = "TXN-" + (lastID + 1).ToString("D6") });
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
            int? PayeeID, string? FirstName, string? LastName,
            string? MiddleName, string? Suffix, string? ContactNumber,
            string? ResidenceAddress, string OfficialReceipt,
            DateTime DateIssued, string? Remarks, string? PaymentMethod,
            int TypeID, decimal TotalBaseAmount, decimal TotalSurcharge,
            decimal TotalInterest, decimal TotalAmount, int CollectedBy_UserID)
        {
            var userID = HttpContext.Session.GetInt32("UserID");
            if (userID == null) return RedirectToAction("Login", "Account");

            if (DateIssued.Date > DateTime.Today)
            {
                TempData["Error"] = "Date issued cannot be in the future.";
                await ReloadCreateViewBags();
                return View();
            }

            if (!PayeeID.HasValue && string.IsNullOrWhiteSpace(FirstName))
            {
                TempData["Error"] = "Please select or add a payor first.";
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

            int payeeID;
            if (PayeeID.HasValue && PayeeID.Value > 0)
            {
                payeeID = PayeeID.Value;
            }
            else
            {
                var newPayee = new Payee
                {
                    Firstname = FirstName, Middlename = MiddleName, Lastname = LastName,
                    Suffix = Suffix, ContactNumber = ContactNumber,
                    ResidenceAddress = ResidenceAddress, CreatedAt = DateTime.Now
                };
                _context.Payees.Add(newPayee);
                await _context.SaveChangesAsync();
                payeeID = newPayee.PayeeID;
            }

            var payment = new PaymentRecord
            {
                OfficialReceipt = OfficialReceipt, PayeeID = payeeID,
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

        private async Task ReloadCreateViewBags()
        {
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
        public string? ContactNumber    { get; set; }
        public string? ResidenceAddress { get; set; }
    }
}
