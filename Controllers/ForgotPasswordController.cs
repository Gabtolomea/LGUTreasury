using LGUTreasury.Data;
using LGUTreasury.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Mail;

namespace LGUTreasury.Controllers
{
    public class ForgotPasswordController : Controller
    {
        private readonly ApplicationDbContext _context;

        // ── Gmail SMTP settings — replace with your Gmail and App Password ──
        private const string SenderEmail    = "gtolomea@gmail.com";
        private const string SenderPassword = "ipgymngumkglbgba";   // Gmail App Password
        private const string SenderName     = "LGU Revenue System";

        public ForgotPasswordController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: /ForgotPassword
        public IActionResult Index()
        {
            return View();
        }

        // POST: /ForgotPassword
        [HttpPost]
        public async Task<IActionResult> Index(string EmployeeID)
        {
            if (string.IsNullOrWhiteSpace(EmployeeID))
            {
                TempData["Error"] = "Please enter your Employee ID.";
                return View();
            }

            var user = await _context.UserAccounts
                .FirstOrDefaultAsync(u => u.EmployeeID == EmployeeID);

            if (user == null)
            {
                TempData["Error"] = "No account found with that Employee ID.";
                return View();
            }

            // No email — redirect to contact admin page
            if (string.IsNullOrWhiteSpace(user.Email))
            {
                TempData["UserName"] = $"{user.FirstName} {user.LastName}";
                return RedirectToAction("NoEmail");
            }

            // Generate 6-digit OTP
            var otp = new Random().Next(100000, 999999).ToString();
            user.OtpCode   = otp;
            user.OtpExpiry = DateTime.Now.AddMinutes(10);
            await _context.SaveChangesAsync();

            // Send OTP via Gmail
            try
            {
                var mail    = new MailMessage();
                var client  = new SmtpClient("smtp.gmail.com", 587)
                {
                    EnableSsl   = true,
                    Credentials = new NetworkCredential(SenderEmail, SenderPassword)
                };

                mail.From = new MailAddress(SenderEmail, SenderName);
                mail.To.Add(user.Email);
                mail.Subject = "Your Password Reset OTP";
                mail.IsBodyHtml = true;
                mail.Body = $@"
                    <div style='font-family:Inter,sans-serif;max-width:480px;margin:auto;padding:24px;border:1px solid #e0e0e0;border-radius:12px'>
                        <div style='font-size:18px;font-weight:700;color:#388E3C;margin-bottom:8px'>LGU Revenue Record System</div>
                        <div style='font-size:14px;color:#444;margin-bottom:20px'>Municipality of Ginatilan</div>
                        <p style='font-size:14px;color:#1a1a1a;margin-bottom:16px'>Hi <strong>{user.FirstName}</strong>,</p>
                        <p style='font-size:14px;color:#444;margin-bottom:20px'>Your password reset OTP is:</p>
                        <div style='font-size:36px;font-weight:800;color:#388E3C;letter-spacing:8px;text-align:center;padding:20px;background:#f0fdf4;border-radius:8px;margin-bottom:20px'>{otp}</div>
                        <p style='font-size:13px;color:#888;margin-bottom:8px'>This OTP expires in <strong>10 minutes</strong>.</p>
                        <p style='font-size:13px;color:#888'>If you did not request this, please ignore this email.</p>
                    </div>";

                await client.SendMailAsync(mail);
            }
            catch (Exception ex)
            {
                TempData["Error"] = "Failed to send OTP email. Please try again or contact your admin." + ex.Message;
                return View();
            }

            // Store employee ID in session for the verify step
            HttpContext.Session.SetString("OtpEmployeeID", EmployeeID);

            TempData["Success"] = $"OTP sent to {MaskEmail(user.Email)}";
            return RedirectToAction("VerifyOtp");
        }

        // GET: /ForgotPassword/VerifyOtp
        public IActionResult VerifyOtp()
        {
            if (string.IsNullOrEmpty(HttpContext.Session.GetString("OtpEmployeeID")))
                return RedirectToAction("Index");
            return View();
        }

        // POST: /ForgotPassword/VerifyOtp
        [HttpPost]
        public async Task<IActionResult> VerifyOtp(string OtpCode)
        {
            var employeeID = HttpContext.Session.GetString("OtpEmployeeID");
            if (string.IsNullOrEmpty(employeeID))
                return RedirectToAction("Index");

            var user = await _context.UserAccounts
                .FirstOrDefaultAsync(u => u.EmployeeID == employeeID);

            if (user == null) return RedirectToAction("Index");

            if (user.OtpCode != OtpCode || user.OtpExpiry == null || user.OtpExpiry < DateTime.Now)
            {
                TempData["Error"] = user.OtpExpiry < DateTime.Now
                    ? "OTP has expired. Please request a new one."
                    : "Invalid OTP. Please try again.";
                return View();
            }

            // OTP valid — allow reset
            HttpContext.Session.SetString("OtpVerified", "true");
            return RedirectToAction("ResetPassword");
        }

        // GET: /ForgotPassword/ResetPassword
        public IActionResult ResetPassword()
        {
            if (HttpContext.Session.GetString("OtpVerified") != "true")
                return RedirectToAction("Index");
            return View();
        }

        // POST: /ForgotPassword/ResetPassword
        [HttpPost]
        public async Task<IActionResult> ResetPassword(string NewPassword, string ConfirmPassword)
        {
            if (HttpContext.Session.GetString("OtpVerified") != "true")
                return RedirectToAction("Index");

            if (string.IsNullOrWhiteSpace(NewPassword) || NewPassword.Length < 6)
            {
                TempData["Error"] = "Password must be at least 6 characters.";
                return View();
            }

            if (NewPassword != ConfirmPassword)
            {
                TempData["Error"] = "Passwords do not match.";
                return View();
            }

            var employeeID = HttpContext.Session.GetString("OtpEmployeeID");
            var user = await _context.UserAccounts
                .FirstOrDefaultAsync(u => u.EmployeeID == employeeID);

            if (user == null) return RedirectToAction("Index");

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(NewPassword);
            user.OtpCode      = null;
            user.OtpExpiry    = null;
            await _context.SaveChangesAsync();

            // Clear session flags
            HttpContext.Session.Remove("OtpEmployeeID");
            HttpContext.Session.Remove("OtpVerified");

            TempData["Success"] = "Password reset successfully! You can now log in.";
            return RedirectToAction("Login", "Account");
        }

        // GET: /ForgotPassword/NoEmail
        public IActionResult NoEmail()
        {
            return View();
        }

        // GET: /ForgotPassword/ResendOtp
        public async Task<IActionResult> ResendOtp()
        {
            var employeeID = HttpContext.Session.GetString("OtpEmployeeID");
            if (string.IsNullOrEmpty(employeeID))
                return RedirectToAction("Index");

            var user = await _context.UserAccounts
                .FirstOrDefaultAsync(u => u.EmployeeID == employeeID);

            if (user == null || string.IsNullOrWhiteSpace(user.Email))
                return RedirectToAction("Index");

            var otp = new Random().Next(100000, 999999).ToString();
            user.OtpCode   = otp;
            user.OtpExpiry = DateTime.Now.AddMinutes(10);
            await _context.SaveChangesAsync();

            try
            {
                var mail   = new MailMessage();
                var client = new SmtpClient("smtp.gmail.com", 587)
                {
                    EnableSsl   = true,
                    Credentials = new NetworkCredential(SenderEmail, SenderPassword)
                };
                mail.From = new MailAddress(SenderEmail, SenderName);
                mail.To.Add(user.Email);
                mail.Subject    = "Your Password Reset OTP (Resent)";
                mail.IsBodyHtml = true;
                mail.Body = $@"
                    <div style='font-family:Inter,sans-serif;max-width:480px;margin:auto;padding:24px;border:1px solid #e0e0e0;border-radius:12px'>
                        <div style='font-size:18px;font-weight:700;color:#388E3C;margin-bottom:8px'>LGU Revenue Record System</div>
                        <p style='font-size:14px;color:#1a1a1a;margin-bottom:16px'>Hi <strong>{user.FirstName}</strong>, here is your new OTP:</p>
                        <div style='font-size:36px;font-weight:800;color:#388E3C;letter-spacing:8px;text-align:center;padding:20px;background:#f0fdf4;border-radius:8px;margin-bottom:20px'>{otp}</div>
                        <p style='font-size:13px;color:#888'>This OTP expires in <strong>10 minutes</strong>.</p>
                    </div>";
                await client.SendMailAsync(mail);
            }
            catch
            {
                TempData["Error"] = "Failed to resend OTP. Please try again.";
                return RedirectToAction("VerifyOtp");
            }

            TempData["Success"] = "A new OTP has been sent to your email.";
            return RedirectToAction("VerifyOtp");
        }

        private static string MaskEmail(string email)
        {
            var parts = email.Split('@');
            if (parts.Length != 2) return email;
            var name   = parts[0];
            var masked = name.Length <= 2 ? "***" : name[0] + new string('*', name.Length - 2) + name[^1];
            return $"{masked}@{parts[1]}";
        }
    }
}
