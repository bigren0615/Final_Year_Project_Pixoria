using System.ComponentModel.DataAnnotations;

namespace Final_Year_Project.Models.Account
{
    public class LoginViewModel
    {
        [Required]
        [Display(Name = "Email or Username")]
        public string Identifier { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Remember me")]
        public bool RememberMe { get; set; }
    }
}
