using Final_Year_Project.Enums;

namespace Final_Year_Project.Models.CommissionPlans
{
    public class RequestArtworkVM
    {
        public int RAId { get; set; }
        public DateTime CreatedAt { get; set; }
        public string ImgName { get; set; } = string.Empty;
        public string? Url { get; set; }
        public requestArtworkType Type { get; set; }
        public string? Comment { get; set; }
    }
}
