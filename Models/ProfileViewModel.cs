using System;
using System.ComponentModel.DataAnnotations;

namespace LGUTreasury.Models
{
    public class ProfileViewModel
    {
        public int UserID { get; set; }

        public string? EmployeeID { get; set; }

        public string? Role { get; set; }

        public string? Status { get; set; }

        public DateTime CreatedAt { get; set; }

        public string? ProfilePicture { get; set; }

        [Required]
        public string? FirstName { get; set; }

        public string? MiddleName { get; set; }

        [Required]
        public string? LastName { get; set; }

        public string? Suffix { get; set; }

        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        public string? Email { get; set; }

        [ContactNumber]
        public string? ContactNumber { get; set; }

        public string? Address { get; set; }
    }
}
