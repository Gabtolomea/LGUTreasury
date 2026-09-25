using LGUTreasury.Data;
using LGUTreasury.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
 
namespace LGUTreasury.Controllers
{
    public class RevenueTypeController : Controller
    {
        private readonly ApplicationDbContext _context;
 
        public RevenueTypeController(ApplicationDbContext context)
        {
            _context = context;
        }
 
        // GET: /RevenueType/Index
        public async Task<IActionResult> Index()
        {
            var userID = HttpContext.Session.GetInt32("UserID");
            if (userID == null)
                return RedirectToAction("Login", "Account");
 
            ViewData["ActiveNav"] = "revtype";
            ViewData["FullName"] = HttpContext.Session.GetString("FullName");
            ViewData["Role"] = HttpContext.Session.GetString("Role");
            ViewData["Initials"] = GetInitials(ViewData["FullName"]?.ToString());
 
            var types = await _context.RevenueTypes
                .Include(r => r.Category)
                .Include(r => r.RevenuePolicies)
                .Where(r => r.IsActive)
                .OrderBy(r => r.TypeID)
                .ToListAsync();
 
            ViewBag.Categories = await _context.RevenueCategories.ToListAsync();
 
            return View(types);
        }
 
        // POST: /RevenueType/Add
        [HttpPost]
        public async Task<IActionResult> Add(string Name, string CategoryID, decimal BaseRate, decimal SurchargeRate, decimal InterestRate)
        {
            var userID = HttpContext.Session.GetInt32("UserID");
            if (userID == null) return Json(new { success = false, message = "Not logged in." });
 
            if (string.IsNullOrWhiteSpace(Name))
                return Json(new { success = false, message = "Name is required." });
 
            var type = new RevenueType
            {
                Name = Name,
                CategoryID = CategoryID,
                BaseRate = BaseRate,
                IsActive = true,
                IsRecurring = false
            };
            _context.RevenueTypes.Add(type);
            await _context.SaveChangesAsync();
 
            var policy = new RevenuePolicy
            {
                TypeID = type.TypeID,
                SurchargeRate = SurchargeRate,
                InterestRate = InterestRate
            };
            _context.RevenuePolicies.Add(policy);
            await _context.SaveChangesAsync();
 
            return Json(new {
                success = true,
                typeID = type.TypeID,
                name = type.Name,
                categoryID = type.CategoryID,
                baseRate = type.BaseRate,
                surchargeRate = policy.SurchargeRate,
                interestRate = policy.InterestRate
            });
        }
 
        // POST: /RevenueType/Edit
        [HttpPost]
        public async Task<IActionResult> Edit(int TypeID, string Name, string CategoryID, decimal BaseRate, decimal SurchargeRate, decimal InterestRate)
        {
            var userID = HttpContext.Session.GetInt32("UserID");
            if (userID == null) return Json(new { success = false, message = "Not logged in." });
 
            var type = await _context.RevenueTypes.FindAsync(TypeID);
            if (type == null) return Json(new { success = false, message = "Revenue type not found." });
 
            type.Name = Name;
            type.CategoryID = CategoryID;
            type.BaseRate = BaseRate;
 
            var policy = await _context.RevenuePolicies.FirstOrDefaultAsync(p => p.TypeID == TypeID);
            if (policy == null)
            {
                policy = new RevenuePolicy { TypeID = TypeID };
                _context.RevenuePolicies.Add(policy);
            }
            policy.SurchargeRate = SurchargeRate;
            policy.InterestRate = InterestRate;
 
            await _context.SaveChangesAsync();
 
            return Json(new { success = true });
        }
 
        // POST: /RevenueType/Delete
        [HttpPost]
        public async Task<IActionResult> Delete(int TypeID)
        {
            var userID = HttpContext.Session.GetInt32("UserID");
            if (userID == null) return Json(new { success = false, message = "Not logged in." });
 
            var type = await _context.RevenueTypes.FindAsync(TypeID);
            if (type == null) return Json(new { success = false, message = "Not found." });
 
            // Hard delete policies first
            var policies = _context.RevenuePolicies.Where(p => p.TypeID == TypeID);
            _context.RevenuePolicies.RemoveRange(policies);
 
            _context.RevenueTypes.Remove(type);
            await _context.SaveChangesAsync();
 
            return Json(new { success = true });
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