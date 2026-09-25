using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace LGUTreasury.Models
{
    public class RevenueCategory
    {
        [Key]
        public string? CategoryID { get; set; }

        [Required]
        public string? Name { get; set; }
        public string? Description { get; set; }
    }
}