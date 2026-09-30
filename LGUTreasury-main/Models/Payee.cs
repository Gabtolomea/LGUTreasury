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

        [NotMapped]
        public string FullName
        {
            get
            {
                var parts = new[] { Firstname, Middlename, Lastname, Suffix };
                var joined = string.Join(" ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));
                return string.IsNullOrWhiteSpace(joined) ? "—" : joined;
            }
        }

        public const int MaxFullNameLength = 300;

        /// <summary>
        /// Prefers the name captured when the payment was encoded, falling back to the
        /// payor's current name for rows recorded before the snapshot existed.
        /// </summary>
        public static string ResolveName(string? payorFullName, Payee? payee)
            => string.IsNullOrWhiteSpace(payorFullName) ? payee?.FullName ?? "—" : payorFullName.Trim();
    }
}