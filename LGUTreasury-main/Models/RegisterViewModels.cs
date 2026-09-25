using System.ComponentModel.DataAnnotations;

namespace LGUTreasury.Models
{
    public class RegisterViewModel
    {
        [Required]
        public string? EmployeeID { get; set; }

        [Required]
        public string? FirstName { get; set; }

        public string? MiddleName { get; set; }

        [Required]
        public string? LastName { get; set; }

        public string? Suffix { get; set; }

        
        public string? Role { get; set; }
        
        public string? Email { get; set; }
        public string? ContactNumber { get; set; }

        public string? Address { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string? Password { get; set; }

        [Required]
        [DataType(DataType.Password)]
        [Compare("Password", ErrorMessage = "Passwords do not match.")]
        public string? ConfirmPassword { get; set; }
    }
}