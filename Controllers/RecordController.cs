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

        // GET: /Record/SearchPayee
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

        // POST: /Record/SavePayee
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

        // GET: /Record/GetNextTransactionID
        [HttpGet]
        public async Task<IActionResult> GetNextTransactionID()
        {
            var lastID = await _context.PaymentRecords
                .OrderByDescending(p => p.PaymentID)
                .Select(p => p.PaymentID)
                .FirstOrDefaultAsync();
            return Json(new { transactionID = "TXN-" + (lastID + 1).ToString("D6") });
        }

        // GET: /Record/GetPaymentDetails
        [HttpGet]
        public async Task<IActionResult> GetPaymentDetails(int paymentID)
        {
            var payment = await _context.PaymentRecords
                .Include(p => p.RecordLineItems).ThenInclude(l => l.RevenueType)
                .FirstOrDefaultAsync(p => p.PaymentID == paymentID);

            if (payment == null) return Json(new { success = false });

            return Json(new {
                success = true,
                officialReceipt = payment.OfficialReceipt,
                dateIssued = payment.DateIssued.ToString("yyyy-MM-dd"),
                paymentMethod = payment.PaymentMethod,
                remarks = payment.Remarks,
                totalAmount = payment.TotalAmount,
                typeID = payment.RecordLineItems?.FirstOrDefault()?.TypeID,
                typeName = payment.RecordLineItems?.FirstOrDefault()?.RevenueType?.Name
            });
        }

        // GET: /Record/Create
        public async Task<IActionResult> Create()
        {
            var userID = HttpContext.Session.GetInt32("UserID");
            if (userID == null) return RedirectToAction("Login", "Account");

            var role = HttpContext.Session.GetString("Role");
            if (role == "Collector") return RedirectToAction("Index");

            ViewData["ActiveNav"] = "record";
            ViewData["FullName"] = HttpContext.Session.GetString("FullName");
            ViewData["Role"] = role;
            ViewData["Initials"] = GetInitials(ViewData["FullName"]?.ToString());

            ViewBag.RevenueTypes = await _context.RevenueTypes
                .Include(r => r.RevenuePolicies)
                .Where(r => r.IsActive)
                .Select(r => new {
                    r.TypeID, r.Name, r.BaseRate,
                    SurchargeRate = r.RevenuePolicies.FirstOrDefault() != null ? r.RevenuePolicies.FirstOrDefault()!.SurchargeRate : 0,
                    InterestRate  = r.RevenuePolicies.FirstOrDefault() != null ? r.RevenuePolicies.FirstOrDefault()!.InterestRate  : 0
                })
                .ToListAsync();

            return View();
        }

        // POST: /Record/Create
        [HttpPost]
        public async Task<IActionResult> Create(
            int? PayeeID, string? FirstName, string? LastName,
            string? MiddleName, string? Suffix, string? ContactNumber,
            string? ResidenceAddress, string OfficialReceipt,
            DateTime DateIssued, string? Remarks, string? PaymentMethod,
            int TypeID, decimal TotalBaseAmount, decimal TotalSurcharge,
            decimal TotalInterest, decimal TotalAmount)
        {
            var userID = HttpContext.Session.GetInt32("UserID");
            if (userID == null) return RedirectToAction("Login", "Account");

            if (DateIssued.Date > DateTime.Today)
            {
                TempData["Error"] = "Date issued cannot be in the future.";
                await ReloadRevenueTypes();
                return View();
            }

            if (!PayeeID.HasValue && string.IsNullOrWhiteSpace(FirstName))
            {
                TempData["Error"] = "Please select or add a payor first.";
                await ReloadRevenueTypes();
                return View();
            }

            if (TypeID == 0)
            {
                TempData["Error"] = "Please select a collection type.";
                await ReloadRevenueTypes();
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
                DateIssued = DateIssued, CollectedBy_UserID = userID.Value,
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

        // GET: /Record/Index
        public async Task<IActionResult> Index()
        {
            var userID = HttpContext.Session.GetInt32("UserID");
            if (userID == null) return RedirectToAction("Login", "Account");

            var role = HttpContext.Session.GetString("Role");
            ViewData["ActiveNav"] = "records";
            ViewData["FullName"] = HttpContext.Session.GetString("FullName");
            ViewData["Role"] = role;
            ViewData["Initials"] = GetInitials(ViewData["FullName"]?.ToString());

            var records = await _context.PaymentRecords
                .Include(p => p.Payee).Include(p => p.CollectedBy)
                .Include(p => p.RecordLineItems).ThenInclude(l => l.RevenueType)
                .OrderByDescending(p => p.DateIssued)
                .ToListAsync();

            if (role == "Officer" || role == "Admin")
            {
                ViewBag.EditRequests = await _context.Editrequests
                    .Include(r => r.PaymentRecord).ThenInclude(p => p.Payee)
                    .Include(r => r.RequestedBy)
                    .OrderByDescending(r => r.CreatedAt)
                    .ToListAsync();
            }

            ViewBag.RevenueTypes = await _context.RevenueTypes
                .Where(r => r.IsActive)
                .Select(r => new { r.TypeID, r.Name })
                .ToListAsync();

            return View(records);
        }

        // POST: /Record/RequestModification — Collector submits proposed changes
        [HttpPost]
        public async Task<IActionResult> RequestModification(
            int PaymentID, string? Reason,
            string? ProposedOR, string? ProposedDate,
            int? ProposedTypeID, string? ProposedPaymentMethod,
            string? ProposedRemarks, decimal? ProposedAmount)
        {
            var userID = HttpContext.Session.GetInt32("UserID");
            if (userID == null) return Json(new { success = false, message = "Not logged in." });

            var payment = await _context.PaymentRecords.FindAsync(PaymentID);
            if (payment == null) return Json(new { success = false, message = "Payment not found." });

            if (payment.HasPendingRequest)
                return Json(new { success = false, message = "This record already has a pending request." });

            var request = new EditRequest
            {
                PaymentID = PaymentID,
                RequestedBy_UserID = userID.Value,
                Reason = Reason,
                ProposedOR = ProposedOR,
                ProposedDate = string.IsNullOrWhiteSpace(ProposedDate) ? null : DateTime.Parse(ProposedDate),
                ProposedTypeID = ProposedTypeID,
                ProposedPaymentMethod = ProposedPaymentMethod,
                ProposedRemarks = ProposedRemarks,
                ProposedAmount = ProposedAmount,
                Status = "Pending",
                CreatedAt = DateTime.Now
            };
            _context.Editrequests.Add(request);
            payment.HasPendingRequest = true;
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        // POST: /Record/ApproveRequest — Officer applies proposed changes
        [HttpPost]
        public async Task<IActionResult> ApproveRequest(int RequestID)
        {
            var userID = HttpContext.Session.GetInt32("UserID");
            if (userID == null) return Json(new { success = false, message = "Not logged in." });

            var request = await _context.Editrequests
                .Include(r => r.PaymentRecord)
                    .ThenInclude(p => p.RecordLineItems)
                .FirstOrDefaultAsync(r => r.RequestID == RequestID);

            if (request == null) return Json(new { success = false, message = "Request not found." });

            if (request.PaymentRecord != null)
            {
                if (!string.IsNullOrWhiteSpace(request.ProposedOR))
                    request.PaymentRecord.OfficialReceipt = request.ProposedOR;

                if (request.ProposedDate.HasValue)
                    request.PaymentRecord.DateIssued = request.ProposedDate.Value;

                if (!string.IsNullOrWhiteSpace(request.ProposedPaymentMethod))
                    request.PaymentRecord.PaymentMethod = request.ProposedPaymentMethod;

                if (request.ProposedRemarks != null)
                    request.PaymentRecord.Remarks = request.ProposedRemarks;

                if (request.ProposedTypeID.HasValue && request.ProposedTypeID.Value > 0)
                {
                    var lineItem = request.PaymentRecord.RecordLineItems?.FirstOrDefault();
                    if (lineItem != null) lineItem.TypeID = request.ProposedTypeID.Value;
                }

                if (request.ProposedAmount.HasValue && request.ProposedAmount.Value > 0)
                    request.PaymentRecord.TotalAmount = request.ProposedAmount.Value;

                request.PaymentRecord.HasPendingRequest = false;
            }

            request.Status = "Approved";
            request.ReviewedBy_UserID = userID.Value;
            request.ReviewedAt = DateTime.Now;
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        // POST: /Record/RejectRequest
        [HttpPost]
        public async Task<IActionResult> RejectRequest(int RequestID, string? ReviewNote)
        {
            var userID = HttpContext.Session.GetInt32("UserID");
            if (userID == null) return Json(new { success = false, message = "Not logged in." });

            var request = await _context.Editrequests
                .Include(r => r.PaymentRecord)
                .FirstOrDefaultAsync(r => r.RequestID == RequestID);

            if (request == null) return Json(new { success = false, message = "Request not found." });

            request.Status = "Rejected";
            request.ReviewedBy_UserID = userID.Value;
            request.ReviewNote = ReviewNote;
            request.ReviewedAt = DateTime.Now;

            if (request.PaymentRecord != null)
                request.PaymentRecord.HasPendingRequest = false;

            await _context.SaveChangesAsync();
            return Json(new { success = true });
        }

        private async Task ReloadRevenueTypes()
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
        public string FirstName { get; set; } = "";
        public string? MiddleName { get; set; }
        public string LastName { get; set; } = "";
        public string? Suffix { get; set; }
        public string? ContactNumber { get; set; }
        public string? ResidenceAddress { get; set; }
    }
}
