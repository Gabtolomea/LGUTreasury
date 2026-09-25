using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LGUTreasury.Models
{
    public class MonthlyBill
    {
        [Key]
        public int MonthlyBillID { get; set; }

        public int LongTermPayeeID { get; set; }
        public int AccountBillingTypeID { get; set; }

        public string? BillingMonth { get; set; }    // e.g. "2026-05"
        public string? BillingType { get; set; }     // e.g. "Water Fees"

        [Column(TypeName = "decimal(12,2)")]
        public decimal BilledAmount { get; set; }

        public string? ORNumber { get; set; }        // filled when marked paid
        public string Status { get; set; } = "Unpaid"; // Unpaid or Paid
        public DateTime? PaidAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation
        public LongTermPayee? LongTermPayee { get; set; }
        public AccountBillingType? AccountBillingType { get; set; }
    }
}