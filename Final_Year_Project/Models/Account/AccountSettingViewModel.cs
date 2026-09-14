using Final_Year_Project.Services;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using AllowedValuesAttribute = Final_Year_Project.Services.AllowedValuesAttribute;

namespace Final_Year_Project.Models.Account
{
    public class AccountSettingViewModel
    {
        public int UId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;

        // New profile fields
        [ValidBirthday(minYear: 1900, minAge: 13, maxAge: 120)]
        public DateOnly? DOB { get; set; }

        [Required(ErrorMessage = "Gender is required")]
        [AllowedValues("male", "female", "other")]
        public string Gender { get; set; } = string.Empty;

        // URL for displaying current profile picture
        public string? ProfilePicUrl { get; set; }

        // File upload for new profile picture
        [AllowedFileExtensions(new[] { ".jpg", ".jpeg", ".png" }, 15)]
        public IFormFile? ProfilePicFile { get; set; }
    }
}
