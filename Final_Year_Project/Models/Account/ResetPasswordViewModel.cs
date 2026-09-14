using System.ComponentModel.DataAnnotations;

namespace Final_Year_Project.Models.Account
{
    public class ResetPasswordViewModel
    {
        [Required]
        public string Token { get; set; } = string.Empty;

        [Display(Name = "New Password")]
        [Required(ErrorMessage = "New password is required.")]
        [StringLength(30, MinimumLength = 8, ErrorMessage = "Password must be between 8–30 characters.")]
        [RegularExpression("^(?=.*[a-z])(?=.*[A-Z])(?=.*\\d)[a-zA-Z\\d!@#$%^&*()_=+-]*$", ErrorMessage = "Must contain uppercase, lowercase, and digit.")]
        public string NewPassword { get; set; } = string.Empty;

        [Display(Name = "Confirm Password")]
        [Required(ErrorMessage = "Confirm password is required.")]
        [Compare("NewPassword", ErrorMessage = "New password and Confirm password do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
