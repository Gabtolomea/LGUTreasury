
using LGUTreasury.Data;
using LGUTreasury.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LGUTreasury.Controllers
{
    public class ProfileController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProfileController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /Profile
        public async Task<IActionResult> Index()
        {
            var user = await GetCurrentUserAsync();
            if (user == null)
                return RedirectToAction("Login", "Account");

            SetViewData(user);
            return View(ToViewModel(user));
        }

        // POST: /Profile/Update
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Update(ProfileViewModel model)
        {
            var user = await GetCurrentUserAsync();
            if (user == null)
                return RedirectToAction("Login", "Account");

            if (!ModelState.IsValid)
            {
                SetViewData(user);
                FillReadOnlyFields(model, user);
                return View("Index", model);
            }

            user.FirstName = model.FirstName?.Trim();
            user.MiddleName = string.IsNullOrWhiteSpace(model.MiddleName) ? null : model.MiddleName.Trim();
            user.LastName = model.LastName?.Trim();
            user.Suffix = string.IsNullOrWhiteSpace(model.Suffix) ? null : model.Suffix.Trim();
            user.Email = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim();
            user.ContactNumber = ContactNumberRules.Normalize(model.ContactNumber);
            user.Address = string.IsNullOrWhiteSpace(model.Address) ? null : model.Address.Trim();

            await _context.SaveChangesAsync();

            HttpContext.Session.SetString("FullName", $"{user.FirstName} {user.LastName}");

            TempData["Success"] = "Your profile has been updated.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<UserAccount?> GetCurrentUserAsync()
        {
            var userID = HttpContext.Session.GetInt32("UserID");
            if (userID == null)
                return null;

            var user = await _context.UserAccounts.FindAsync(userID);
            if (user == null)
                HttpContext.Session.Clear();

            return user;
        }

        private void SetViewData(UserAccount user)
        {
            var fullName = $"{user.FirstName} {user.LastName}".Trim();

            ViewData["ActiveNav"] = "profile";
            ViewData["FullName"] = fullName;
            ViewData["Role"] = user.Role;
            ViewData["Initials"] = GetInitials(fullName);
        }

        private static ProfileViewModel ToViewModel(UserAccount user)
        {
            return new ProfileViewModel
            {
                UserID = user.UserID,
                EmployeeID = user.EmployeeID,
                Role = user.Role,
                Status = user.Status,
                CreatedAt = user.CreatedAt,
                FirstName = user.FirstName,
                MiddleName = user.MiddleName,
                LastName = user.LastName,
                Suffix = user.Suffix,
                Email = user.Email,
                ContactNumber = ContactNumberRules.Format(user.ContactNumber),
                Address = user.Address
            };
        }

        private static void FillReadOnlyFields(ProfileViewModel model, UserAccount user)
        {
            model.UserID = user.UserID;
            model.EmployeeID = user.EmployeeID;
            model.Role = user.Role;
            model.Status = user.Status;
            model.CreatedAt = user.CreatedAt;
        }

        private static string GetInitials(string? fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return "?";

            var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (parts.Length >= 2)
                return $"{parts[0][0]}{parts[^1][0]}".ToUpper();

            return parts[0][0].ToString().ToUpper();
        }
    }
}
