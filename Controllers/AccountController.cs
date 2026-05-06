using LGUTreasury.Data;
using LGUTreasury.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;


namespace LGUTreasury.Controllers
{
    
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AccountController(ApplicationDbContext context)
        {
            _context = context;
        }
        // GET: /Account/Login
public IActionResult Login()
{
    Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
    Response.Headers["Pragma"] = "no-cache";
    Response.Headers["Expires"] = "0";

    if (HttpContext.Session.GetInt32("UserID") != null)
        return RedirectToAction("Index", "Home");

    return View();
}

        // POST: /Account/Login
        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _context.UserAccounts
                .FirstOrDefaultAsync(u => u.EmployeeID == model.EmployeeID);

            if (user == null || !BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash))
            {
                ModelState.AddModelError("", "Invalid ID or password.");
                return View(model);
            }

            // Store session
            HttpContext.Session.SetInt32("UserID", user.UserID);
            HttpContext.Session.SetString("Role", user.Role!);
            HttpContext.Session.SetString("FullName", $"{user.FirstName} {user.LastName}");

            return RedirectToAction("Index", "Home");
        }

        // GET: /Account/Register
        public IActionResult Register()
        {
            return View();
        }

        // POST: /Account/Register
        [HttpPost]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            // Check if EmployeeID already exists
            var exists = await _context.UserAccounts
                .AnyAsync(u => u.EmployeeID == model.EmployeeID);

            if (exists)
            {
                ModelState.AddModelError("EmployeeID", "ID Number already exists.");
                return View(model);
            }

            var user = new UserAccount
            {
                EmployeeID = model.EmployeeID,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
                Role = model.Role,
                FirstName = model.FirstName,
                MiddleName = model.MiddleName,
                LastName = model.LastName,
                Suffix = model.Suffix,
                ContactNumber = model.ContactNumber,
                Address = model.Address,
                IsActive = true,
                CreatedAt = DateTime.Now
            };

            _context.UserAccounts.Add(user);
            await _context.SaveChangesAsync();

            return RedirectToAction("Login");
        }

        // GET: /Account/Logout
        public async Task<IActionResult> Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login");
        }
    }
}