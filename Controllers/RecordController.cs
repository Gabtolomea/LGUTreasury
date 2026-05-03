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
                .Where(r => r.IsActive)
                .ToListAsync();

            return View();
        }

        // POST: /Record/Create
        [HttpPost]
        public async Task<IActionResult> Create(
            string FirstName, string LastName,
            string? MiddleName, string? Suffix, string? ContactNumber,
            string? ResidenceAddress, string OfficialReceipt,
            DateTime DateIssued, string? Remarks,
            List<int> TypeIDs, List<int> Quantities,
            List<decimal> BaseAmounts,
            List<decimal?> SurchargeAmounts,
            List<decimal?> InterestAmounts)
        {
            var userID = HttpContext.Session.GetInt32("UserID");
            if (userID == null)
                return RedirectToAction("Login", "Account");
                if (DateIssued.Date > DateTime.Today)
           {
             TempData["Error"] = "Date issued cannot be in the future.";
              ViewBag.RevenueTypes = await _context.RevenueTypes.Where(r => r.IsActive).ToListAsync();
             return View();
          }

            var payee = new Payee
            {
                Firstname = FirstName,
                Middlename = MiddleName,
                Lastname = LastName,
                Suffix = Suffix,
                ContactNumber = ContactNumber,
                ResidenceAddress = ResidenceAddress,
                CreatedAt = DateTime.Now
            };
            _context.Payees.Add(payee);
            await _context.SaveChangesAsync();

            var payment = new PaymentRecord
            {
                OfficialReceipt = OfficialReceipt,
                PayeeID = payee.PayeeID,
                DateIssued = DateIssued,
                CollectedBy_UserID = userID.Value,
                Remarks = Remarks,
                CreatedAt = DateTime.Now
            };

            decimal totalBase = 0, totalSurcharge = 0, totalInterest = 0;
            var lineItems = new List<RecordLineItem>();

            for (int i = 0; i < TypeIDs.Count; i++)
            {
                var baseAmt = BaseAmounts[i];
                var surcharge = SurchargeAmounts.Count > i ? SurchargeAmounts[i] ?? 0 : 0;
                var interest = InterestAmounts.Count > i ? InterestAmounts[i] ?? 0 : 0;
                var qty = Quantities[i];
                var lineTotal = (baseAmt + surcharge + interest) * qty;

                totalBase += baseAmt * qty;
                totalSurcharge += surcharge * qty;
                totalInterest += interest * qty;

                lineItems.Add(new RecordLineItem
                {
                    TypeID = TypeIDs[i],
                    Quantity = qty,
                    BaseAmount = baseAmt,
                    SurchargeAmount = surcharge,
                    InterestAmount = interest,
                    LineTotal = lineTotal
                });
            }

            payment.TotalBaseAmount = totalBase;
            payment.TotalSurcharge = totalSurcharge;
            payment.TotalInterest = totalInterest;
            payment.TotalAmount = totalBase + totalSurcharge + totalInterest;

            _context.PaymentRecords.Add(payment);
            await _context.SaveChangesAsync();

            foreach (var item in lineItems)
            {
                item.PaymentID = payment.PaymentID;
                _context.RecordLineItems.Add(item);
            }
            await _context.SaveChangesAsync();

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
                .OrderByDescending(p => p.DateIssued)
                .ToListAsync();

            return View(records);
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