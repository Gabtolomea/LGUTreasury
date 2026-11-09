using LGUTreasury.Data;
using LGUTreasury.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LGUTreasury.Controllers
{
    public class ProfileController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;

        private static readonly string[] AllowedPhotoExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        private const long MaxPhotoBytes = 5 * 1024 * 1024;
        private const string PhotoFolder = "uploads/profile-pictures";

        public ProfileController(ApplicationDbContext context, IWebHostEnvironment env)
        {
            _context = context;
            _env = env;
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

            user.FirstName     = model.FirstName?.Trim();
            user.MiddleName    = string.IsNullOrWhiteSpace(model.MiddleName) ? null : model.MiddleName.Trim();
            user.LastName      = model.LastName?.Trim();
            user.Suffix        = string.IsNullOrWhiteSpace(model.Suffix) ? null : model.Suffix.Trim();
            user.Email         = string.IsNullOrWhiteSpace(model.Email) ? null : model.Email.Trim();
            user.ContactNumber = ContactNumberRules.Normalize(model.ContactNumber);
            user.Address       = string.IsNullOrWhiteSpace(model.Address) ? null : model.Address.Trim();

            await _context.SaveChangesAsync();

            HttpContext.Session.SetString("FullName", $"{user.FirstName} {user.LastName}");

            TempData["Success"] = "Your profile has been updated.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Profile/UploadPhoto
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadPhoto(IFormFile? photo)
        {
            var user = await GetCurrentUserAsync();
            if (user == null)
                return RedirectToAction("Login", "Account");

            if (photo == null || photo.Length == 0)
            {
                TempData["Error"] = "Please choose an image to upload.";
                return RedirectToAction(nameof(Index));
            }

            if (photo.Length > MaxPhotoBytes)
            {
                TempData["Error"] = "Profile picture must be 5 MB or smaller.";
                return RedirectToAction(nameof(Index));
            }

            var extension = Path.GetExtension(photo.FileName).ToLowerInvariant();
            if (!AllowedPhotoExtensions.Contains(extension) ||
                string.IsNullOrWhiteSpace(photo.ContentType) ||
                !photo.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Only JPG, PNG, GIF, or WEBP images are allowed.";
                return RedirectToAction(nameof(Index));
            }

            var folder = Path.Combine(_env.WebRootPath, PhotoFolder.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(folder);

            var fileName = $"user-{user.UserID}-{Guid.NewGuid():N}{extension}";
            var fullPath = Path.Combine(folder, fileName);

            await using (var stream = System.IO.File.Create(fullPath))
            {
                await photo.CopyToAsync(stream);
            }

            DeletePhotoFile(user.ProfilePicture);

            user.ProfilePicture = $"/{PhotoFolder}/{fileName}";
            await _context.SaveChangesAsync();

            TempData["Success"] = "Profile picture updated.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Profile/RemovePhoto
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemovePhoto()
        {
            var user = await GetCurrentUserAsync();
            if (user == null)
                return RedirectToAction("Login", "Account");

            DeletePhotoFile(user.ProfilePicture);

            user.ProfilePicture = null;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Profile picture removed.";
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
            ViewData["ActiveNav"]   = "profile";
            ViewData["FullName"]    = fullName;
            ViewData["Role"]        = user.Role;
            ViewData["Initials"]    = GetInitials(fullName);
            ViewData["ProfilePicture"] = user.ProfilePicture;
        }

        private static ProfileViewModel ToViewModel(UserAccount user)
        {
            return new ProfileViewModel
            {
                UserID         = user.UserID,
                EmployeeID     = user.EmployeeID,
                Role           = user.Role,
                Status         = user.Status,
                CreatedAt      = user.CreatedAt,
                ProfilePicture = user.ProfilePicture,
                FirstName      = user.FirstName,
                MiddleName     = user.MiddleName,
                LastName       = user.LastName,
                Suffix         = user.Suffix,
                Email          = user.Email,
                ContactNumber  = ContactNumberRules.Format(user.ContactNumber),
                Address        = user.Address
            };
        }

        private static void FillReadOnlyFields(ProfileViewModel model, UserAccount user)
        {
            model.UserID         = user.UserID;
            model.EmployeeID     = user.EmployeeID;
            model.Role           = user.Role;
            model.Status         = user.Status;
            model.CreatedAt      = user.CreatedAt;
            model.ProfilePicture = user.ProfilePicture;
        }

        private void DeletePhotoFile(string? relativePath)
        {
            if (string.IsNullOrWhiteSpace(relativePath))
                return;

            var trimmed = relativePath.TrimStart('/', '\\')
                .Replace('/', Path.DirectorySeparatorChar);

            var fullPath = Path.GetFullPath(Path.Combine(_env.WebRootPath, trimmed));
            var root = Path.GetFullPath(_env.WebRootPath);

            if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                return;

            if (System.IO.File.Exists(fullPath))
                System.IO.File.Delete(fullPath);
        }

        private static string GetInitials(string? fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName)) return "?";
            var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 2) return $"{parts[0][0]}{parts[^1][0]}".ToUpper();
            return parts[0][0].ToString().ToUpper();
        }
    }
}
