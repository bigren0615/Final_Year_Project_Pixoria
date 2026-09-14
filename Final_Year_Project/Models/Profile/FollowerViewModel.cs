namespace Final_Year_Project.Models.Profile
{
    public class FollowerViewModel
    {
        public int FId { get; set; }
        public DateTime FDate { get; set; }
        public int FollowerId { get; set; }
        public int ArtistId { get; set; }
        public string? DisplayName { get; set; }
        public string? ProfilePicUrl { get; set; }
    }
}
