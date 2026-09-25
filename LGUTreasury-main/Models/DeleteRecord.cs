using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LGUTreasury.Models
{

public class DeletedRecord
{
    public int DeletedRecordID { get; set; }

    // FK back to the original payment — needed for restore
    public int PaymentID { get; set; }
    public PaymentRecord? PaymentRecord { get; set; }

    // Snapshot strings so info still shows even if related data changes
    public string? PayeeName { get; set; }
    public string? CollectorName { get; set; }
    public string? CollectionType { get; set; }

    // Who deleted it and when
    public string? DeletedByName { get; set; }
    public int DeletedBy_UserID { get; set; }
    public UserAccount? DeletedBy { get; set; }
    public DateTime DeletedAt { get; set; }
}
}