using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LGUTreasury.Models
{
    public class RevenuePolicy
    {
        [Key]
        public int PolicyID { get; set; }

        [Required]
        public int TypeID { get; set; }

        [Column(TypeName = "decimal(5,2)")]
        public decimal SurchargeRate { get; set; } = 0;

        [Column(TypeName = "decimal(5,2)")]
        public decimal InterestRate { get; set; } = 0;

        public DateOnly? Deadline { get; set; }

        public string? Notes { get; set; }

        [ForeignKey("TypeID")]
        public RevenueType? RevenueType { get; set; }
    }
}