namespace Final_Year_Project.Models.Posts
{
    public class HomePostItemVM
    {
        public int PostId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Content { get; set; }
        public string? CoverImageUrl { get; set; }
        public int ArtistId { get; set; }
        public string ArtistName { get; set; } = string.Empty;
        public string? ArtistProfilePic { get; set; }
        public DateTime CreatedAt { get; set; }
        public int DaysAgo { get; set; }
        public string? PlanName { get; set; }
        public decimal? Price { get; set; }
    }
}
