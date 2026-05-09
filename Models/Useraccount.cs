using System;
using System.ComponentModel.DataAnnotations;

namespace LGUTreasury.Models
{
    public class UserAccount
    {
        [Key]
        public int UserID { get; set; }

        [Required]
        public string ? EmployeeID { get; set; }

        [Required]
        public string ? PasswordHash { get; set; }

        [Required]
        public string ? Role { get; set; }

        [Required]
        public string ? FirstName { get; set; }

        public string ? MiddleName { get; set; }

        [Required]
        public string ? LastName { get; set; }

        public string ? Suffix { get; set; }

        public string ? ContactNumber { get; set; }

        public string ? Address { get; set; }

        public bool IsActive { get; set; } = true;

        public string Status { get; set; } = "Pending";

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}