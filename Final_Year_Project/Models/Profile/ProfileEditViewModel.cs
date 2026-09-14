using Final_Year_Project.Services;
using System.ComponentModel.DataAnnotations;
using AllowedValuesAttribute = Final_Year_Project.Services.AllowedValuesAttribute;

namespace Final_Year_Project.Models.Profile
{
    public class ProfileEditViewModel
    {
        public int UserId { get; set; }
        [MaxLength(15)]
        [Required(ErrorMessage = "Nickname is required")]
        public string Nickname { get; set; } = string.Empty;
        [ValidBirthday(minYear: 1900, minAge: 13, maxAge: 100)]
        public DateOnly? DOB { get; set; }
        [Required(ErrorMessage = "Gender is required")]
        [AllowedValues("male", "female", "other")]
        public string Gender { get; set; } = string.Empty;
        [MaxLength(1000)]
        public string? AboutMe { get; set; } = string.Empty;
        public string? ProfilePicUrl { get; set; }
        [AllowedFileExtensions(new[] { ".jpg", ".jpeg", ".png" }, 15)]
        public IFormFile? ProfilePicFile { get; set; }
        public string? BannerImageUrl { get; set; }
        [AllowedFileExtensions(new[] { ".jpg", ".jpeg", ".png" }, 15)]
        public IFormFile? BannerImageFile { get; set; }
        [AllowedFileExtensions(new[] { ".jpg", ".jpeg", ".png" }, 15)]
        public List<IFormFile>? ArtworkFiles { get; set; } = new();
        public string? ArtworksToDelete { get; set; }
        public List<ProfileArtworkAddEditViewModel> Artworks { get; set; } = new List<ProfileArtworkAddEditViewModel>();

    }

    public class ProfileArtworkAddEditViewModel
    {
        public int ProfArtid { get; set; }
        public IFormFile? ImageFile { get; set; }
        public string? ImageUrl { get; set; }
        public int UserId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
