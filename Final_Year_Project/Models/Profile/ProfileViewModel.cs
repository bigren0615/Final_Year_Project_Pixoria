using Final_Year_Project.Enums;
using Final_Year_Project.Models.CommissionPlans;
using Final_Year_Project.Models.Sales;

namespace Final_Year_Project.Models.Profile
{
    public class ProfileViewModel
    {
        public int UserId { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Nickname { get; set; } = string.Empty;
        public DateOnly? DOB { get; set; }
        public usersRole Role { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? ProfilePic { get; set; }
        public string? ProfilePicUrl { get; set; }
        public bool IsOwner { get; set; }
        public string? BannerImageUrl { get; set; }
        public string? AboutMe { get; set; } = string.Empty;
        public List<ProfileArtworkViewModel> Artworks { get; set; } = new List<ProfileArtworkViewModel>();
        public List<SubscriptionPlanViewModel>? SubscriptionPlans { get; set; }
        public List<PostViewModel>? Posts { get; set; }
        public List<PostViewModel>? LatestPosts { get; set; }
        public List<CommissionPlanViewModel>? CommissionPlans { get; set; }
        public List<ArtistProductDetailsViewModel>? Products { get; set; }
        public bool IsFollow { get; set; }
    }

    public class ProfileArtworkViewModel
    {
        public int ProfArtid { get; set; }
        public string? ImageUrl { get; set; }
    }

    public class SubscriptionPlanViewModel
    {
        public int SPId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public string? ImageUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool Subscribed { get; set; }
    }

    public class PostViewModel
    {
        public int PostId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Content { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string Status { get; set; } = string.Empty;
        public int? SPId { get; set; }
        public string? CoverImageUrl { get; set; }
        public decimal? Price { get; set; }
        public string? PlanTitle { get; set; }
    }
}
