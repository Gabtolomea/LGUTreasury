using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LGUTreasury.Models
{
    public class RecordLineItem
    {
        [Key]
        public int LineItemID { get; set; }

        [Required]
        public int PaymentID { get; set; }

        [Required]
        public int TypeID { get; set; }

        public int Quantity { get; set; } = 1;

        [Column(TypeName = "decimal(12,2)")]
        public decimal BaseAmount { get; set; } = 0;

        [Column(TypeName = "decimal(12,2)")]
        public decimal SurchargeAmount { get; set; } = 0;

        [Column(TypeName = "decimal(12,2)")]
        public decimal InterestAmount { get; set; } = 0;

        [Column(TypeName = "decimal(12,2)")]
        public decimal LineTotal { get; set; }

        [ForeignKey("PaymentID")]
        public PaymentRecord? PaymentRecord { get; set; }

        [ForeignKey("TypeID")]
        public RevenueType? RevenueType { get; set; }
    }
}