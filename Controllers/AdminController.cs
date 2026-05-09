using LGUTreasury.Data;
using LGUTreasury.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LGUTreasury.Controllers
{
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Admin/AccountRequests
        public async Task<IActionResult> AccountRequests()
        {
            var userID = HttpContext.Session.GetInt32("UserID");
            if (userID == null)
                return RedirectToAction("Login", "Account");

            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin")
                return RedirectToAction("Index", "Home");

            ViewData["ActiveNav"] = "accountrequests";
            ViewData["FullName"] = HttpContext.Session.GetString("FullName");
            ViewData["Role"] = role;
            ViewData["Initials"] = GetInitials(ViewData["FullName"]?.ToString());

            var pendingAccounts = await _context.UserAccounts
                .Where(u => u.Status == "Pending")
                .OrderBy(u => u.CreatedAt)
                .ToListAsync();

            return View(pendingAccounts);
        }

        // POST: /Admin/ApproveAccount
        [HttpPost]
        public async Task<IActionResult> ApproveAccount(int userID, string role)
        {
            var adminID = HttpContext.Session.GetInt32("UserID");
            if (adminID == null)
                return Json(new { success = false, message = "Not logged in." });

            var adminRole = HttpContext.Session.GetString("Role");
            if (adminRole != "Admin")
                return Json(new { success = false, message = "Unauthorized." });

            var user = await _context.UserAccounts.FindAsync(userID);
            if (user == null)
                return Json(new { success = false, message = "Account not found." });

            user.Status = "Active";
            user.Role = role;
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        // POST: /Admin/RejectAccount
        [HttpPost]
        public async Task<IActionResult> RejectAccount(int userID)
        {
            var adminID = HttpContext.Session.GetInt32("UserID");
            if (adminID == null)
                return Json(new { success = false, message = "Not logged in." });

            var adminRole = HttpContext.Session.GetString("Role");
            if (adminRole != "Admin")
                return Json(new { success = false, message = "Unauthorized." });

            var user = await _context.UserAccounts.FindAsync(userID);
            if (user == null)
                return Json(new { success = false, message = "Account not found." });

            _context.UserAccounts.Remove(user);
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
