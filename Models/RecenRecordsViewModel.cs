namespace LGUTreasury.Models
{
    public class RecentRecordViewModel
    {
        public string Initials { get; set; } = "";
        public string PayeeName { get; set; } = "";
        public string Type { get; set; } = "";
        public string? ReceiptNo { get; set; }
        public string Amount { get; set; } = "";
        public string Time { get; set; } = "";
    }
}