using System.ComponentModel.DataAnnotations;

namespace Final_Year_Project.Models.Account
{
    public class RegisterViewModel
    {
            [Required(ErrorMessage = "Username is required")]
            public string Username { get; set; } = string.Empty;

            [Required(ErrorMessage = "Name is required")]
            public string Name { get; set; } = string.Empty;

            public string Nickname { get; set; } = string.Empty;

            [Required(ErrorMessage = "Gender is required")]
            public string Gender { get; set; } = string.Empty;

            [Required(ErrorMessage = "Email is required")]
            [EmailAddress]
            public string Email { get; set; } = string.Empty;

            [Required(ErrorMessage = "Phone Number is required")]
            public string PhoneNumber { get; set; } = string.Empty;

            [Required(ErrorMessage = "Password is required")]
            [DataType(DataType.Password)]
            public string Password { get; set; } = string.Empty;

            [Required(ErrorMessage = "Password Confirmation is required")]
            [DataType(DataType.Password)]
            public string ConfirmPassword { get; set; } = string.Empty;

            public string? Role { get; set; }
    }
}
