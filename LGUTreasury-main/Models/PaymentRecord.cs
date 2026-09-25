using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
 
namespace LGUTreasury.Models
{
    public class PaymentRecord
    {
        [Key]
        public int PaymentID { get; set; }
 
        [Required]
        public string? OfficialReceipt { get; set; }
 
        [Required]
        public int PayeeID { get; set; }
 
        [Required]
        public DateTime DateIssued { get; set; }
 
        [Required]
        public int CollectedBy_UserID { get; set; }
 
        [Column(TypeName = "decimal(12,2)")]
        public decimal TotalBaseAmount { get; set; } = 0;
 
        [Column(TypeName = "decimal(12,2)")]
        public decimal TotalSurcharge { get; set; } = 0;
 
        [Column(TypeName = "decimal(12,2)")]
        public decimal TotalInterest { get; set; } = 0;
 
        [Column(TypeName = "decimal(12,2)")]
        public decimal TotalAmount { get; set; }
 
        public string? PaymentMethod { get; set; }
 
        public string? Remarks { get; set; }
 
        public bool HasPendingRequest { get; set; } = false;
 
        public DateTime CreatedAt { get; set; } = DateTime.Now;
 
        [ForeignKey("PayeeID")]
        public Payee? Payee { get; set; }
 
        [ForeignKey("CollectedBy_UserID")]
        public UserAccount? CollectedBy { get; set; }
 
        public ICollection<RecordLineItem> RecordLineItems { get; set; } = new List<RecordLineItem>();
 
        public ICollection<EditRequest> EditRequests { get; set; } = new List<EditRequest>();

        public bool IsCollected { get; set; } = false;
        public DateTime? CollectedConfirmedAt { get; set; }

        public bool IsDeleted { get; set; } = false;
    }
}