using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LGUTreasury.Models
{
    public class RevenueType
    {
        [Key]
        public int TypeID { get; set; }

        [Required]
        public string? CategoryID { get; set; }

        [Required]
        public string? Name { get; set; }

        [Column(TypeName = "decimal(10,2)")]
        public decimal BaseRate { get; set; }

        public bool IsRecurring { get; set; } = false;

        public bool IsActive { get; set; } = true;

        public string? OrdinanceSection { get; set; }

        [ForeignKey("CategoryID")]
        public RevenueCategory? Category { get; set; }

        public ICollection<RevenuePolicy> RevenuePolicies { get; set; } = new List<RevenuePolicy>();
    }
}