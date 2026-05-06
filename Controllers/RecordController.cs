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
                .Where(p => p.Firstname!.Contains(query) ||
                            p.Lastname!.Contains(query))
                .Take(8)
                .Select(p => new {
                    p.PayeeID,
                    p.Firstname,
                    p.Middlename,
                    p.Lastname,
                    p.Suffix,
                    p.ContactNumber,
                    p.ResidenceAddress
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
                Firstname = req.FirstName,
                Middlename = req.MiddleName,
                Lastname = req.LastName,
                Suffix = req.Suffix,
                ContactNumber = req.ContactNumber,
                ResidenceAddress = req.ResidenceAddress,
                CreatedAt = DateTime.Now
            };
            _context.Payees.Add(payee);
            await _context.SaveChangesAsync();
 
            return Json(new {
                success = true,
                payee = new {
                    payeeID = payee.PayeeID,
                    firstname = payee.Firstname,
                    middlename = payee.Middlename,
                    lastname = payee.Lastname,
                    suffix = payee.Suffix,
                    contactNumber = payee.ContactNumber,
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
 
        // GET: /Record/Create
        public async Task<IActionResult> Create()
        {
            var userID = HttpContext.Session.GetInt32("UserID");
            if (userID == null)
                return RedirectToAction("Login", "Account");
 
            ViewData["ActiveNav"] = "record";
            ViewData["FullName"] = HttpContext.Session.GetString("FullName");
            ViewData["Role"] = HttpContext.Session.GetString("Role");
            ViewData["Initials"] = GetInitials(ViewData["FullName"]?.ToString());
 
            ViewBag.RevenueTypes = await _context.RevenueTypes
                .Include(r => r.RevenuePolicies)
                .Where(r => r.IsActive)
                .Select(r => new {
                    r.TypeID,
                    r.Name,
                    r.BaseRate,
                    SurchargeRate = r.RevenuePolicies.FirstOrDefault() != null ? r.RevenuePolicies.FirstOrDefault()!.SurchargeRate : 0,
                    InterestRate  = r.RevenuePolicies.FirstOrDefault() != null ? r.RevenuePolicies.FirstOrDefault()!.InterestRate  : 0
                })
                .ToListAsync();
 
            return View();
        }
 
        // POST: /Record/Create
        [HttpPost]
        public async Task<IActionResult> Create(
            int? PayeeID,
            string? FirstName, string? LastName,
            string? MiddleName, string? Suffix, string? ContactNumber,
            string? ResidenceAddress, string OfficialReceipt,
            DateTime DateIssued, string? Remarks, string? PaymentMethod,
            int TypeID,
            decimal TotalBaseAmount, decimal TotalSurcharge,
            decimal TotalInterest, decimal TotalAmount)
        {
            var userID = HttpContext.Session.GetInt32("UserID");
            if (userID == null)
                return RedirectToAction("Login", "Account");
 
            // Validate date
            if (DateIssued.Date > DateTime.Today)
            {
                TempData["Error"] = "Date issued cannot be in the future.";
                await ReloadRevenueTypes();
                return View();
            }
 
            // Validate payor
            if (!PayeeID.HasValue && string.IsNullOrWhiteSpace(FirstName))
            {
                TempData["Error"] = "Please select or add a payor first.";
                await ReloadRevenueTypes();
                return View();
            }
 
            // Validate collection type
            if (TypeID == 0)
            {
                TempData["Error"] = "Please select a collection type.";
                await ReloadRevenueTypes();
                return View();
            }
 
            // Use existing payee or create new one
            int payeeID;
            if (PayeeID.HasValue && PayeeID.Value > 0)
            {
                payeeID = PayeeID.Value;
            }
            else
            {
                var newPayee = new Payee
                {
                    Firstname = FirstName,
                    Middlename = MiddleName,
                    Lastname = LastName,
                    Suffix = Suffix,
                    ContactNumber = ContactNumber,
                    ResidenceAddress = ResidenceAddress,
                    CreatedAt = DateTime.Now
                };
                _context.Payees.Add(newPayee);
                await _context.SaveChangesAsync();
                payeeID = newPayee.PayeeID;
            }
 
            // Create payment record
            var payment = new PaymentRecord
            {
                OfficialReceipt = OfficialReceipt,
                PayeeID = payeeID,
                DateIssued = DateIssued,
                CollectedBy_UserID = userID.Value,
                Remarks = Remarks,
                PaymentMethod = PaymentMethod,
                TotalBaseAmount = TotalBaseAmount,
                TotalSurcharge = TotalSurcharge,
                TotalInterest = TotalInterest,
                TotalAmount = TotalAmount,
                CreatedAt = DateTime.Now
            };
            _context.PaymentRecords.Add(payment);
            await _context.SaveChangesAsync();
 
            // Create single line item
            var lineItem = new RecordLineItem
            {
                PaymentID = payment.PaymentID,
                TypeID = TypeID,
                Quantity = 1,
                BaseAmount = TotalBaseAmount,
                SurchargeAmount = TotalSurcharge,
                InterestAmount = TotalInterest,
                LineTotal = TotalAmount
            };
            _context.RecordLineItems.Add(lineItem);
            await _context.SaveChangesAsync();
 
            TempData["Toast"] = "Payment recorded successfully!";
            return RedirectToAction("Index");
        }
 
        // GET: /Record/Index
        public async Task<IActionResult> Index()
        {
            var userID = HttpContext.Session.GetInt32("UserID");
            if (userID == null)
                return RedirectToAction("Login", "Account");
 
            ViewData["ActiveNav"] = "records";
            ViewData["FullName"] = HttpContext.Session.GetString("FullName");
            ViewData["Role"] = HttpContext.Session.GetString("Role");
            ViewData["Initials"] = GetInitials(ViewData["FullName"]?.ToString());
 
            var records = await _context.PaymentRecords
                .Include(p => p.Payee)
                .Include(p => p.CollectedBy)
                .Include(p => p.RecordLineItems)
                    .ThenInclude(l => l.RevenueType)
                .OrderByDescending(p => p.DateIssued)
                .ToListAsync();
 
            return View(records);
        }
 
        // Helper to reload revenue types into ViewBag
        private async Task ReloadRevenueTypes()
        {
            ViewBag.RevenueTypes = await _context.RevenueTypes
                .Include(r => r.RevenuePolicies)
                .Where(r => r.IsActive)
                .Select(r => new {
                    r.TypeID,
                    r.Name,
                    r.BaseRate,
                    SurchargeRate = r.RevenuePolicies.FirstOrDefault() != null ? r.RevenuePolicies.FirstOrDefault()!.SurchargeRate : 0,
                    InterestRate  = r.RevenuePolicies.FirstOrDefault() != null ? r.RevenuePolicies.FirstOrDefault()!.InterestRate  : 0
                })
                .ToListAsync();
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
 