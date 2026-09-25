using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;   

namespace LGUTreasury.Models
{
    public class Payee
    {
      [Key]
      public int PayeeID {get; set;}  
      [Required]
      public string? Firstname {get; set;}
      public string? Middlename {get; set;}
      [Required]
      public string? Lastname {get; set;}
        public string? Suffix {get; set;}
        public string? ContactNumber {get; set;}        
        public string? ResidenceAddress {get; set;}
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}