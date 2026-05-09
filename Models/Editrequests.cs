using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
 
namespace LGUTreasury.Models
{
    public class EditRequest
    {
        [Key]
        public int RequestID { get; set; }
 
        [Required]
        public int PaymentID { get; set; }
 
        [Required]
        public int RequestedBy_UserID { get; set; }
 
        public string? Reason { get; set; }
 
        [Required]
        public string Status { get; set; } = "Pending";
        // Values: "Pending", "Approved", "Rejected"
 
        public int? ReviewedBy_UserID { get; set; }
 
        public string? ReviewNote { get; set; }
 
        public DateTime CreatedAt { get; set; } = DateTime.Now;
 
        public DateTime? ReviewedAt { get; set; }
 
        [ForeignKey("PaymentID")]
        public PaymentRecord? PaymentRecord { get; set; }
 
        [ForeignKey("RequestedBy_UserID")]
        public UserAccount? RequestedBy { get; set; }
 
        [ForeignKey("ReviewedBy_UserID")]
        public UserAccount? ReviewedBy { get; set; }

       public string? ProposedOR { get; set; }
       public DateTime? ProposedDate { get; set; }
       public int? ProposedTypeID { get; set; }
       public string? ProposedPaymentMethod { get; set; }
       public string? ProposedRemarks { get; set; }
       public decimal? ProposedAmount { get; set; }
    }
}