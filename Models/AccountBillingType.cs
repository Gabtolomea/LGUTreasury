using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LGUTreasury.Models
{
    public class AccountBillingType
    {
        [Key]
        public int AccountBillingTypeID { get; set; }

        public int LongTermPayeeID { get; set; }

        [Required]
        public string BillingTypeName { get; set; } = "";

        [Column(TypeName = "decimal(12,2)")]
        public decimal MonthlyRate { get; set; }

        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation
        public LongTermPayee? LongTermPayee { get; set; }
        public ICollection<MonthlyBill>? MonthlyBills { get; set; }
    }
}