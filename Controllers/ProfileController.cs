
using LGUTreasury.Data;
using LGUTreasury.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LGUTreasury.Controllers
{
    public class ProfileController : Controller
    {
        private const long MaxPhotoBytes = 2 * 1024 * 1024;

        private static readonly string[] AllowedPhotoExtensions =
            { ".png", ".jpg", ".jpeg", ".gif", ".webp" };

        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public ProfileController(ApplicationDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
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
                TempData["Error"] = "The photo is too large. Please choose an image under 2 MB.";
                return RedirectToAction(nameof(Index));
            }

            var extension = Path.GetExtension(photo.FileName).ToLowerInvariant();
            if (!AllowedPhotoExtensions.Contains(extension))
            {
                TempData["Error"] = "Unsupported file type. Please upload a PNG, JPG, GIF or WEBP image.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var uploadsRoot = GetProfilePicturesRoot();
                Directory.CreateDirectory(uploadsRoot);

                var fileName = $"{user.UserID}_{Guid.NewGuid():N}{extension}";
                var filePath = Path.Combine(uploadsRoot, fileName);

                await using (var stream = System.IO.File.Create(filePath))
                {
                    await photo.CopyToAsync(stream);
                }

                var previousPicture = user.ProfilePicture;

                user.ProfilePicture = $"/uploads/profile-pictures/{fileName}";
                await _context.SaveChangesAsync();

                DeletePhotoFile(previousPicture);

                TempData["Success"] = "Your profile picture has been updated.";
            }
            catch (IOException)
            {
                TempData["Error"] = "We could not save your profile picture. Please try again.";
            }
            catch (UnauthorizedAccessException)
            {
                TempData["Error"] = "We could not save your profile picture. Please try again.";
            }

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

            if (string.IsNullOrWhiteSpace(user.ProfilePicture))
            {
                TempData["Info"] = "You do not have a profile picture to remove.";
                return RedirectToAction(nameof(Index));
            }

            var previousPicture = user.ProfilePicture;

            user.ProfilePicture = null;
            await _context.SaveChangesAsync();

            DeletePhotoFile(previousPicture);

            TempData["Success"] = "Your profile picture has been removed.";
            return RedirectToAction(nameof(Index));
        }

        private string GetWebRootPath()
        {
            return string.IsNullOrWhiteSpace(_environment.WebRootPath)
                ? Path.Combine(_environment.ContentRootPath, "wwwroot")
                : _environment.WebRootPath;
        }

        private string GetProfilePicturesRoot()
        {
            return Path.Combine(GetWebRootPath(), "uploads", "profile-pictures");
        }

        private void DeletePhotoFile(string? picturePath)
        {
            if (string.IsNullOrWhiteSpace(picturePath))
                return;

            var webRoot = GetWebRootPath();
            var uploadsRoot = Path.GetFullPath(Path.Combine(webRoot, "uploads"));
            var relativePath = picturePath.TrimStart('/', '\\')
                .Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.GetFullPath(Path.Combine(webRoot, relativePath));

            // Only ever delete files that live inside the uploads folder.
            if (!fullPath.StartsWith(uploadsRoot, StringComparison.OrdinalIgnoreCase))
                return;

            try
            {
                if (System.IO.File.Exists(fullPath))
                    System.IO.File.Delete(fullPath);
            }
            catch (IOException)
            {
                // A stale file is harmless; leave it behind rather than failing the request.
            }
            catch (UnauthorizedAccessException)
            {
                // Same as above.
            }
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
                Address = user.Address,
                ProfilePicture = user.ProfilePicture
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
