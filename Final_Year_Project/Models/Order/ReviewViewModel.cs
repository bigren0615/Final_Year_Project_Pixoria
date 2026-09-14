namespace Final_Year_Project.Models.OrderHistory
{
    public class ReviewViewModel
    {
        public string? PId { get; set; }

        public int Star { get; set; }
        public string? Comment { get; set; }

        public string? ProductName { get; set; }
        public string? ImagePath { get; set; }
        public string Username { get; set; } = "";
        public string? ProfileImage { get; set; }
    }
}
