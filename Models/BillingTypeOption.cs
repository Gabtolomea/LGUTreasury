using System.ComponentModel.DataAnnotations;

namespace LGUTreasury.Models
{
    public class BillingTypeOption
    {
        [Key]
        public int BillingTypeOptionID { get; set; }

        [Required]
        public string Name { get; set; } = "";

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}