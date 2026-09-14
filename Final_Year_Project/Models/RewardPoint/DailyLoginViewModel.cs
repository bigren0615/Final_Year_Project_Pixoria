using Final_Year_Project.Models.DB;

namespace Final_Year_Project.Models.RewardPoint
{
    public class DailyLoginViewModel
    {
        public bool HasClaimedToday { get; set; }
        public int TotalRewardPoints { get; set; }
        public int DailyRewardPoints { get; set; } = 10;
        public List<PointLog> PointHistory { get; set; } = new List<PointLog>();
    }
}
