using Final_Year_Project.Enums;
using Final_Year_Project.Models.Profile;

namespace Final_Year_Project.Models.Posts
{
    public class PostsViewModel
    {
        public int PostId { get; set; }
        public int UId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Content { get; set; } = string.Empty;
        public postsStatus Status { get; set; }
        public int? SPId { get; set; }
        public string? CoverImageUrl { get; set; }
        public bool IsOwner { get; set; }
        public decimal? Price { get; set; }
        public ProfileViewModel? ArtistProfile { get; set; }
        public bool IsSubscribed { get; set; }
        public string? PlanName { get; set; }
        public decimal? PlanPrice { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
        public int LikeCount { get; set; }
        public bool IsLiked { get; set; }
    }
}
