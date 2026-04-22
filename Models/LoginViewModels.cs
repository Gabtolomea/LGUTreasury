using System.ComponentModel.DataAnnotations;

namespace LGUTreasury.Models
{
    public class LoginViewModel
    {
        [Required]
        public string? EmployeeID { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string? Password { get; set; }
    }
}