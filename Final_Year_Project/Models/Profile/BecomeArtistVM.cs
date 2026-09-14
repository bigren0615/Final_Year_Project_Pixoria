using System.ComponentModel.DataAnnotations;
using Final_Year_Project.Services;

namespace Final_Year_Project.Models.Profile
{
    public class BecomeArtistVM
    {
        [Required(ErrorMessage = "Artist nickname is required")]
        [Display(Name = "Artist Nickname")]
        public string? Nickname { get; set; }

        [MustBeTrue(ErrorMessage = "You must agree to the Terms of Use to continue.")]
        public bool AgreeToTerms { get; set; }
    }
}
