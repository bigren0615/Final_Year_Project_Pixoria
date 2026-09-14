using Final_Year_Project.Enums;
using Final_Year_Project.Models;
using Final_Year_Project.Models.DB;
using Final_Year_Project.Models.Posts;
using Final_Year_Project.Models.Product;
using Final_Year_Project.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Diagnostics;
using System.Security.Claims;
using System.Xml.Linq;
using static Supabase.Postgrest.Constants;

namespace Final_Year_Project.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly SupabaseService _supabaseService;
        private readonly LocalStorageService _storageService;
        private readonly EnumService _enumService;

        public HomeController(ILogger<HomeController> logger, SupabaseService supabaseService, LocalStorageService storageService, EnumService enumService)
        {
            _logger = logger;
            _supabaseService = supabaseService;
            _storageService = storageService;
            _enumService = enumService;
        }

        public async Task<IActionResult> Index()
        {
            // Redirect admin users to Fraud Detection dashboard
            var role = User.FindFirstValue(ClaimTypes.Role);
            if (role == "admin")
            {
                return RedirectToAction("ScanAndMonitor", "ArtworkFraudDetection");
            }

            var sw = Stopwatch.StartNew();
            var client = _supabaseService.GetClient();
            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int? uid = string.IsNullOrEmpty(uidValue) ? null : int.Parse(uidValue!);

            var homeViewModel = new HomePageVM { FollowedSubscribedPosts = new(), LatestPosts = new() };

            if (uid.HasValue)
            {
                var followedResponse = await client
                    .From<Follow>()
                    .Filter("follower_id", Operator.Equals, uid.Value)
                    .Get();
                var followedIds = followedResponse.Models.Select(f => f.ArtistId).ToList();

                var subscriptionPlansResponse = await client
                    .From<SubscriptionPlan>()
                    .Get();
                var allPlans = subscriptionPlansResponse.Models.ToList();

                var subscribedResponse = await client
                    .From<Subscription>()
                    .Filter("UId", Operator.Equals, uid.Value)
                    .Filter("status", Operator.Equals, _enumService.ToStringValue(subscriptionStatus.active))
                    .Get();
                var subscribedPlanIds = subscribedResponse.Models.Select(s => s.SPId).Distinct().ToList();
                var subscribedArtistIds = allPlans
                    .Where(p => subscribedPlanIds.Contains(p.SPId))
                    .Select(p => p.UId)
                    .ToList();

                var followedSubscribedIds = followedIds.Concat(subscribedArtistIds).Distinct().ToList();

                if (followedSubscribedIds.Any())
                {
                    var followedPostsResponse = await client
                        .From<Posts>()
                        .Filter("UId", Operator.In, followedSubscribedIds)
                        .Filter("status", Operator.Equals, _enumService.ToStringValue(postsStatus.published))
                        .Order("created_at", Ordering.Descending)
                        .Limit(8)
                        .Get();

                    var t1 = Stopwatch.StartNew();
                    homeViewModel.FollowedSubscribedPosts = await BuildPostViewModels(followedPostsResponse.Models.ToList(), client);
                    _logger.LogInformation("BuildPostViewModels (followed) elapsed: {ms}ms", t1.ElapsedMilliseconds);
                }
            }

            // Fetch 20 latest posts from all users
            var allPostsResponse = await client
                .From<Posts>()
                .Filter("status", Operator.Equals, _enumService.ToStringValue(postsStatus.published))
                .Order("created_at", Ordering.Descending)
                .Limit(8)
                .Get();

            var t2 = Stopwatch.StartNew();
            homeViewModel.LatestPosts = await BuildPostViewModels(allPostsResponse.Models.ToList(), client);
            _logger.LogInformation("BuildPostViewModels (latest) elapsed: {ms}ms", t2.ElapsedMilliseconds);

            //var productsResponse = await client.From<Product>().Get();
            //var productImagesResponse = await client.From<ProductImage>().Get();
            //var categoriesResponse = await client.From<Category>().Get();
            //var subCategoriesResponse = await client.From<SubCategory>().Get();

            //var products = productsResponse.Models ?? new List<Product>();
            //var productImages = productImagesResponse.Models;
            //var categories = categoriesResponse.Models ?? new List<Category>();
            //var subCategories = subCategoriesResponse.Models ?? new List<SubCategory>();

            //var filtered = products
            //    .Where(p => !string.IsNullOrWhiteSpace(p.Status) &&
            //                p.Status.Trim().Equals("available", StringComparison.OrdinalIgnoreCase));

            //if (!string.IsNullOrEmpty(name))
            //    filtered = filtered.Where(p => p.ProductName.Contains(name, StringComparison.OrdinalIgnoreCase));

            //if (catId.HasValue)
            //    filtered = filtered.Where(p => p.CatId == catId.Value);

            //if (subCatId.HasValue)
            //    filtered = filtered.Where(p => p.SubCatId == subCatId.Value);

            //var productViewModels = filtered.Select(p =>
            //{
            //    var primaryImage = productImages.FirstOrDefault(i => i.PId == p.PId && i.IsPrimary)?.ImagePath;
            //    return new ProductSalesViewModel
            //    {
            //        PId = p.PId,
            //        ProductName = p.ProductName,
            //        PrimaryImage = primaryImage,
            //        Price = p.Price
            //    };
            //}).ToList();

            //var pagedProducts = productViewModels.ToPagedList(page, 8);

            // Fetch 8 recommended products

            var productsResponse = await client
                .From<Product>()
                .Filter("status", Operator.Equals, "available")
                .Order("created_date", Ordering.Descending)
                .Limit(8)
                .Get();

            var productImagesResponse = await client.From<ProductImage>().Get();

            var products = productsResponse.Models;
            var productImages = productImagesResponse.Models;

            homeViewModel.RecommendedProducts = products.Select(p =>
            {
                var primaryImage = productImages.FirstOrDefault(i => i.PId == p.PId && i.IsPrimary)?.ImagePath;
                return new HomeProductItemVM
                {
                    PId = p.PId!,
                    Name = p.ProductName,
                    Price = p.Price,
                    ImageUrl = primaryImage
                };
            }).ToList();

            sw.Stop();
            _logger.LogInformation("Home.Index total elapsed: {ms}ms", sw.ElapsedMilliseconds);

            return View(homeViewModel);
        }

        private async Task<List<HomePostItemVM>> BuildPostViewModels(List<Posts> posts, Supabase.Client client)
        {
            var result = new List<HomePostItemVM>();
            if (posts == null || posts.Count == 0)
                return result;

            try
            {
                // Batch fetch all distinct artists for the posts to avoid N network calls (one per post)
                var authorIds = posts.Select(p => p.UId).Distinct().ToList();
                var usersResponse = await client
                    .From<Users>()
                    .Filter("UId", Operator.In, authorIds)
                    .Get();

                var usersById = usersResponse.Models?.ToDictionary(u => u.Uid) ?? new Dictionary<int, Users>();

                // Fetch subscription plans for posts that have SPId
                var spIds = posts.Where(p => p.SPId.HasValue && p.SPId.Value > 0).Select(p => p.SPId.Value).Distinct().ToList();
                var plansById = new Dictionary<int, SubscriptionPlan>();
                if (spIds.Any())
                {
                    var plansResponse = await client
                        .From<SubscriptionPlan>()
                        .Filter("SPId", Operator.In, spIds)
                        .Get();
                    plansById = plansResponse.Models?.ToDictionary(sp => sp.SPId) ?? new Dictionary<int, SubscriptionPlan>();
                }

                foreach (var post in posts)
                {
                    try
                    {
                        if (!usersById.TryGetValue(post.UId, out var artist) || artist == null)
                            continue;

                        string? profilePicUrl = null;
                        if (!string.IsNullOrEmpty(artist.ProfilePic))
                            profilePicUrl = _storageService.BuildFileUrl("profile_pic", artist.ProfilePic);

                        string? coverImageUrl = null;
                        if (!string.IsNullOrEmpty(post.CoverImage))
                            coverImageUrl = _storageService.BuildFileUrl("post_cover", post.CoverImage);

                        var displayName = _enumService.ToEnum<usersRole>(artist.Role) == usersRole.artist
                            ? artist.Nickname
                            : artist.Name;

                        // Get plan info if post has a subscription plan
                        string? planName = null;
                        decimal? planPrice = null;
                        if (post.SPId.HasValue && post.SPId.Value > 0 && plansById.TryGetValue(post.SPId.Value, out var plan))
                        {
                            planName = plan.Title;
                            planPrice = plan.Price;
                        }

                        result.Add(new HomePostItemVM
                        {
                            PostId = post.PostId,
                            Title = post.Title ?? "Untitled",
                            Content = post.Content,
                            CoverImageUrl = coverImageUrl,
                            ArtistId = post.UId,
                            ArtistName = displayName ?? "Unknown Artist",
                            ArtistProfilePic = profilePicUrl,
                            CreatedAt = post.CreatedAt,
                            DaysAgo = (int)(DateTime.UtcNow - post.CreatedAt).TotalDays,
                            PlanName = planName,
                            Price = planPrice
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Error building post VM for post {post.PostId}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching authors for posts: {ex.Message}");
            }

            return result;
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
