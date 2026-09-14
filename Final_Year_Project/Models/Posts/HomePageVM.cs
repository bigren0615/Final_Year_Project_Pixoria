namespace Final_Year_Project.Models.Posts
{
    public class HomePageVM
    {
        public List<HomePostItemVM> FollowedSubscribedPosts { get; set; } = new();
        public List<HomePostItemVM> LatestPosts { get; set; } = new();

        public List<HomeProductItemVM> RecommendedProducts { get; set; } = new();
    }
}
