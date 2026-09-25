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
 
        public string? Reason { get; set; }  // ← this is the message from Collector
 
        [Required]
        public string Status { get; set; } = "Pending";
        // Values: "Pending", "Resolved"
 
        public int? ReviewedBy_UserID { get; set; }
 
        public string? ReviewNote { get; set; }  // ← Officer's reply/note
 
        public DateTime CreatedAt { get; set; } = DateTime.Now;
 
        public DateTime? ReviewedAt { get; set; }
 
        [ForeignKey("PaymentID")]
        public PaymentRecord? PaymentRecord { get; set; }
 
        [ForeignKey("RequestedBy_UserID")]
        public UserAccount? RequestedBy { get; set; }
 
        [ForeignKey("ReviewedBy_UserID")]
        public UserAccount? ReviewedBy { get; set; }
    }
}