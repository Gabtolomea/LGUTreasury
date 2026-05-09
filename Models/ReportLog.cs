using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LGUTreasury.Models
{
    public class ReportLog
    {
        [Key]
        public int LogID { get; set; }

        [Required]
        public string ReportType { get; set; } = string.Empty;

        [Required]
        public string Format { get; set; } = string.Empty;

        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

        public int GeneratedByUserID { get; set; }
    }
}
