using System.ComponentModel.DataAnnotations;

namespace LGUTreasury.Models
{
    public class LongTermPayee
    {
        [Key]
        public int LongTermPayeeID { get; set; }

        [Required]
        public string FirstName { get; set; } = "";
        public string? MiddleName { get; set; }

        [Required]
        public string LastName { get; set; } = "";
        public string? Suffix { get; set; }
        public string? ContactNumber { get; set; }
        public string? Address { get; set; }

        [Required]
        public string StartMonth { get; set; } = "";  // e.g. "2026-01"
        public int BillGenerationDay { get; set; } = 20;
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation
        public ICollection<AccountBillingType>? AccountBillingTypes { get; set; }
        public ICollection<MonthlyBill>? MonthlyBills { get; set; }
    }
}