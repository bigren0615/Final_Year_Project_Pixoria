using Final_Year_Project.Enums;
using Final_Year_Project.Models.DB;
using Final_Year_Project.Models.Posts;
using Final_Year_Project.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;
using System.Security.Claims;
using System.Text.Json;
using static Supabase.Postgrest.Constants;

namespace Final_Year_Project.Controllers
{
    [Authorize(Policy = "NonAdminOnly")]
    public class PostsController : Controller
    {
        private readonly SupabaseService _supabaseService;
        private readonly LocalStorageService _storageService;
        private readonly EnumService _enumService;
        public PostsController(SupabaseService supabaseService, LocalStorageService storageService, EnumService enumService)
        {
            _supabaseService = supabaseService;
            _storageService = storageService;
            _enumService = enumService;
        }

        [AllowAnonymous]
        [Route("Posts")]
        public async Task<IActionResult> PostsIndex(string? search = null, string filter = "all", int page = 1, int pageSize = 12)
        {
            SetCacheHeaders();

            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0";
            var uid = int.Parse(uidValue!);
            var client = _supabaseService.GetClient();

            var postsResponse = await client
                .From<Posts>()
                .Filter("status", Operator.Equals, _enumService.ToStringValue(postsStatus.published))
                .Order("created_at", Ordering.Descending)
                .Get();

            var allPosts = postsResponse.Models.ToList();

            var artistIds = allPosts.Select(p => p.UId).Distinct().ToList();
            var artistsResponse = artistIds.Any() 
                ? await client.From<Users>().Filter("UId", Operator.In, artistIds).Get()
                : null;
            var artists = artistsResponse?.Models.ToList() ?? new List<Users>();

            var followedArtistIds = new List<int>();
            var subscribedArtistIds = new List<int>();

            if (uid != 0)
            {
                var followedResponse = await client
                    .From<Follow>()
                    .Filter("follower_id", Operator.Equals, uid)
                    .Get();
                followedArtistIds = followedResponse.Models.Select(f => f.ArtistId).ToList();

                var subscriptionsResponse = await client
                    .From<Subscription>()
                    .Filter("UId", Operator.Equals, uid)
                    .Filter("status", Operator.Equals, _enumService.ToStringValue(subscriptionStatus.active))
                    .Filter("payment_status", Operator.Equals, _enumService.ToStringValue(paymentStatus.paid))
                    .Get();
                var planIds = subscriptionsResponse.Models.Select(s => s.SPId).Distinct().ToList();

                if (planIds.Any())
                {
                    var plansResponse = await client
                        .From<SubscriptionPlan>()
                        .Filter("SPId", Operator.In, planIds)
                        .Get();
                    subscribedArtistIds = plansResponse.Models.Select(p => p.UId).Distinct().ToList();
                }
            }

            var filtered = allPosts.AsEnumerable();

            if (filter == "followed" && uid != 0)
            {
                filtered = filtered.Where(p => followedArtistIds.Contains(p.UId));
            }
            else if (filter == "subscribed" && uid != 0)
            {
                filtered = filtered.Where(p => subscribedArtistIds.Contains(p.UId));
            }
            else if (filter == "both" && uid != 0)
            {
                var combinedIds = followedArtistIds.Union(subscribedArtistIds).ToList();
                filtered = filtered.Where(p => combinedIds.Contains(p.UId));
            }

            // Apply search filter (search by title OR artist nickname/username OR tag)
            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.Trim();
                var matchingPostIdsByTag = new List<int>();

                // Check if this is a tag search (starts with #)
                if (searchLower.StartsWith("#"))
                {
                    // Extract tag name without the # prefix
                    var tagSearch = searchLower.Substring(1);
                    
                    if (!string.IsNullOrWhiteSpace(tagSearch))
                    {
                        // Load tags for filtered posts to enable tag-based search
                        var filteredPostIds = filtered.Select(p => p.PostId).ToList();
                        
                        if (filteredPostIds.Any())
                        {
                            try
                            {
                                var tagPostResponse = await client.From<TagPost>().Filter("post_id", Operator.In, filteredPostIds).Get();
                                var tagPosts = tagPostResponse.Models?.ToList() ?? new List<TagPost>();
                                var tagIds = tagPosts.Select(tp => tp.TagId).Distinct().ToList();
                                
                                if (tagIds.Any())
                                {
                                    // Only search available tags
                                    var tagsResponse = await client.From<Tag>()
                                        .Filter("tag_id", Operator.In, tagIds)
                                        .Filter("status", Operator.Equals, _enumService.ToStringValue(tagStatus.available))
                                        .Get();
                                    var tags = tagsResponse.Models?.Where(t => !string.IsNullOrEmpty(t.TagName)).ToList() ?? new List<Tag>();
                                    
                                    // Find post IDs with matching tags
                                    var matchingTagIds = tags
                                        .Where(t => t.TagName!.Contains(tagSearch, StringComparison.OrdinalIgnoreCase))
                                        .Select(t => t.TagId)
                                        .ToHashSet();
                                    
                                    matchingPostIdsByTag = tagPosts
                                        .Where(tp => matchingTagIds.Contains(tp.TagId))
                                        .Select(tp => tp.PostId)
                                        .Distinct()
                                        .ToList();
                                }
                            }
                            catch (Exception ex)
                            {
                                // Tags are non-critical for search, log but continue without them
                                Console.WriteLine("Failed to load tags for search: " + ex.Message);
                            }
                        }
                    }

                    // Only filter by tags when searching with #
                    filtered = filtered.Where(p => matchingPostIdsByTag.Contains(p.PostId));
                }
                else
                {
                    // Regular search by title or artist name (no tag search)
                    var matchingArtistIds = artists
                        .Where(a => (!string.IsNullOrEmpty(a.Nickname) && a.Nickname.Contains(searchLower, StringComparison.OrdinalIgnoreCase))
                                 || (!string.IsNullOrEmpty(a.Username) && a.Username.Contains(searchLower, StringComparison.OrdinalIgnoreCase)))
                        .Select(a => a.Uid)
                        .ToList();

                    filtered = filtered.Where(p => (p.Title != null && p.Title.Contains(searchLower, StringComparison.OrdinalIgnoreCase))
                                                || matchingArtistIds.Contains(p.UId));
                }
            }

            var filteredList = filtered.ToList();
            int totalCount = filteredList.Count;
            int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            if (page > totalPages && totalPages > 0)
                page = totalPages;

            // Get paginated results
            var postsData = filteredList
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // Fetch subscription plans for posts that have SPId
            var spIds = postsData.Where(p => p.SPId.HasValue && p.SPId.Value > 0).Select(p => p.SPId.Value).Distinct().ToList();
            var plansById = new Dictionary<int, SubscriptionPlan>();
            if (spIds.Any())
            {
                var plansResponse = await client
                    .From<SubscriptionPlan>()
                    .Filter("SPId", Operator.In, spIds)
                    .Get();
                plansById = plansResponse.Models?.ToDictionary(sp => sp.SPId) ?? new Dictionary<int, SubscriptionPlan>();
            }

            // Build view models with artist info
            var postViewModels = postsData.Select(p =>
            {
                var artist = artists.FirstOrDefault(a => a.Uid == p.UId);
                var artistProfilePicUrl = artist != null && !string.IsNullOrEmpty(artist.ProfilePic)
                    ? _storageService.BuildFileUrl("profile_pic", artist.ProfilePic)
                    : null;

                var coverImageUrl = !string.IsNullOrEmpty(p.CoverImage)
                    ? _storageService.BuildFileUrl("post_cover", p.CoverImage)
                    : "/images/no_image.jpg";

                var daysAgo = (DateTime.UtcNow - p.CreatedAt).Days;

                // Get plan info if post has a subscription plan
                string? planName = null;
                decimal? planPrice = null;
                if (p.SPId.HasValue && p.SPId.Value > 0 && plansById.TryGetValue(p.SPId.Value, out var plan))
                {
                    planName = plan.Title;
                    planPrice = plan.Price;
                }

                return new HomePostItemVM
                {
                    PostId = p.PostId,
                    Title = p.Title ?? "",
                    Content = p.Content,
                    CoverImageUrl = coverImageUrl,
                    ArtistId = p.UId,
                    ArtistName = artist?.Nickname ?? artist?.Name ?? "Unknown",
                    ArtistProfilePic = artistProfilePicUrl,
                    CreatedAt = p.CreatedAt,
                    DaysAgo = daysAgo,
                    PlanName = planName,
                    Price = planPrice
                };
            }).ToList();

            ViewData["CurrentPage"] = page;
            ViewData["TotalPages"] = totalPages;
            ViewData["PageSize"] = pageSize;
            ViewData["SearchQuery"] = search;
            // Matched artist count for UI messaging
            ViewData["MatchedArtistCount"] = artists
                .Count(a => (!string.IsNullOrEmpty(a.Nickname) && a.Nickname.Contains(search ?? string.Empty, StringComparison.OrdinalIgnoreCase))
                         || (!string.IsNullOrEmpty(a.Name) && a.Name.Contains(search ?? string.Empty, StringComparison.OrdinalIgnoreCase)));
            ViewData["CurrentFilter"] = filter;
            ViewData["IsAuthenticated"] = uid != 0;

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
                Response.Headers["Vary"] = "X-Requested-With";
                return PartialView("_PostsListPartial", postViewModels);
            }

            return View(postViewModels);
        }

        [Authorize]
        [Route("Posts/manage")]
        public async Task<IActionResult> ManagePosts()
        {
            var redirectResult = RedirectIfNotArtist();
            if (redirectResult != null)
                return redirectResult;

            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidValue))
                return RedirectToAction("Login", "Account");

            var uid = int.Parse(uidValue);

            var client = _supabaseService.GetClient();

            // Fetch all posts for the current user excluding deleted posts.
            var postsResponse = await client
                .From<Posts>()
                .Filter("UId", Operator.Equals, uid)
                .Filter("status", Operator.NotEqual, _enumService.ToStringValue(postsStatus.deleted))
                .Order("updated_at", Ordering.Descending)
                .Get();

            var posts = postsResponse.Models?.ToList() ?? new List<Posts>();

            // Fetch subscription plans for posts that have SPId
            var planIds = posts.Where(p => p.SPId.HasValue).Select(p => (int)p.SPId!).Distinct().ToList();
            var planDict = new Dictionary<int, SubscriptionPlan>();

            if (planIds.Any())
            {
                var plansResponse = await client
                    .From<SubscriptionPlan>()
                    .Filter("SPId", Operator.In, planIds)
                    .Get();

                if (plansResponse.Models != null)
                {
                    planDict = plansResponse.Models.ToDictionary(p => p.SPId, p => p);
                }
            }

            // Fetch like counts for all posts
            var postIds = posts.Select(p => p.PostId).ToList();
            var likeCounts = new Dictionary<int, int>();
            if (postIds.Any())
            {
                var likesResponse = await client
                    .From<PostLike>()
                    .Filter("post_id", Operator.In, postIds)
                    .Get();

                if (likesResponse.Models != null)
                {
                    likeCounts = likesResponse.Models
                        .GroupBy(l => l.PostId)
                        .ToDictionary(g => g.Key, g => g.Count());
                }
            }

            // Map to view model
            var viewModels = posts.Select(p =>
            {
                var coverUrl = string.IsNullOrEmpty(p.CoverImage) ? "/images/no_image.jpg" : _storageService.BuildFileUrl("post_cover", p.CoverImage);
                
                string planName = "All users";
                decimal? planPrice = null;
                
                if (p.SPId.HasValue && planDict.TryGetValue(p.SPId.Value, out var plan))
                {
                    planName = plan.Title ?? "All users";
                    planPrice = plan.Price;
                }

                return new PostsViewModel
                {
                    PostId = p.PostId,
                    UId = p.UId,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt,
                    Title = p.Title ?? string.Empty,
                    Content = p.Content,
                    Status = _enumService.ToEnum<postsStatus>(p.Status),
                    SPId = p.SPId,
                    CoverImageUrl = coverUrl,
                    IsOwner = true, // Current user is owner of posts listed here
                    PlanName = planName,
                    PlanPrice = planPrice,
                    LikeCount = likeCounts.TryGetValue(p.PostId, out var count) ? count : 0
                };
            }).ToList();

            return View(viewModels);
        }

        [AllowAnonymous]
        [Route("Posts/{id:int}")]
        public async Task<IActionResult> Posts(int id)
        {
            SetCacheHeaders();

            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0";
            var uid = int.Parse(uidValue!);
            bool isOwner = false;
            bool isSubscribed = false;
            var client = _supabaseService.GetClient();

            var posts = await client
            .From<Posts>()
            .Where(p => p.PostId == id)
            .Single();

            if (posts == null || posts.Status != _enumService.ToStringValue(postsStatus.published) && posts.Status != _enumService.ToStringValue(postsStatus.@private)) return View("NotFound");

            if (posts.UId == uid)
            {
                isOwner = true;
                isSubscribed = true;
            }
            else
            {
                if (posts.Status == _enumService.ToStringValue(postsStatus.@private)) return View("NotFound");
            }

            string? coverImageUrl = null;
            if (!string.IsNullOrEmpty(posts.CoverImage))
                coverImageUrl = _storageService.BuildFileUrl("post_cover", posts.CoverImage);

            var subscriptionPlan = await client
            .From<SubscriptionPlan>()
            .Where(s => s.SPId == posts.SPId)
            .Single();

            decimal? price = null;
            string? planName = null;
            if (subscriptionPlan != null)
            {
                price = subscriptionPlan.Price;
                planName = subscriptionPlan.Title;
            }

            var artist = await client
            .From<Users>()
            .Where(u => u.Uid == posts.UId)
            .Single();

            if (artist == null)
                return View("NotFound");

            string? artistProfilePicUrl = string.IsNullOrEmpty(artist.ProfilePic)
            ? null
            : _storageService.BuildFileUrl("profile_pic", artist.ProfilePic);

            string? bannerProfileUrl = string.IsNullOrEmpty(artist.BannerImage)
            ? null
            : _storageService.BuildFileUrl("banner_image", artist.BannerImage);

            bool isFollow = false;
            if (uid != 0 && uid != id)
            {
                var followResponse = await client
                    .From<Follow>()
                    .Filter("follower_id", Operator.Equals, uid)
                    .Filter("artist_id", Operator.Equals, posts.UId)
                    .Single();
                if (followResponse != null)
                    isFollow = true;
            }

            var artistProfile = new Models.Profile.ProfileViewModel
            {
                UserId = artist.Uid,
                Username = artist.Username,
                Nickname = artist.Nickname,
                Role = _enumService.ToEnum<usersRole>(artist.Role),
                CreatedAt = artist.CreatedAt,
                ProfilePicUrl = artistProfilePicUrl,
                IsOwner = (uid == artist.Uid),
                IsFollow = isFollow,
                BannerImageUrl = bannerProfileUrl,
            };

            if (posts.SPId == null)
                isSubscribed = true;

            if (!isOwner && posts.SPId != null)
            {
                var subscriptionsTask = await client
                    .From<Subscription>()
                    .Filter("UId", Operator.Equals, uid)
                    .Filter("end_date", Operator.GreaterThan, DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.ffffff+00"))
                    .Filter("status", Operator.Equals, _enumService.ToStringValue(subscriptionStatus.active))
                    .Filter("payment_status", Operator.Equals, _enumService.ToStringValue(paymentStatus.paid))
                    .Get();

                var subscriptions = subscriptionsTask.Models.ToList();

                foreach (var sub in subscriptions)
                {
                    var userPlan = await client
                        .From<SubscriptionPlan>()
                        .Filter("SPId", Operator.Equals, sub.SPId)
                        .Filter("status", Operator.Equals, _enumService.ToStringValue(subscriptionPlanStatus.active))
                        .Single();

                    if (userPlan == null)
                        continue;

                    if (userPlan.UId != artist.Uid)
                        continue;

                    if (userPlan.Price >= subscriptionPlan!.Price && userPlan.UId == subscriptionPlan.UId)
                    {
                        isSubscribed = true;
                        break;
                    }
                }
            }

            // Load tags for this post
            var tagNames = new List<string>();
            try
            {
                var tagPostResp = await client.From<TagPost>().Filter("post_id", Operator.Equals, posts.PostId).Get();
                var tagPosts = tagPostResp.Models?.ToList() ?? new List<TagPost>();
                var tagIds = tagPosts.Select(t => t.TagId).Distinct().ToList();
                if (tagIds.Any())
                {
                    var tagsResp = await client.From<Tag>().Filter("tag_id", Operator.In, tagIds).Get();
                    tagNames = tagsResp.Models?.Where(t => !string.IsNullOrEmpty(t.TagName)).Select(t => t.TagName!).ToList() ?? new List<string>();
                }
            }
            catch { }

            // Get like count and user's like status
            int likeCount = 0;
            bool isLiked = false;
            try
            {
                var likesResponse = await client.From<PostLike>().Filter("post_id", Operator.Equals, posts.PostId).Get();
                likeCount = likesResponse.Models?.Count ?? 0;
                
                if (uid != 0)
                {
                    isLiked = likesResponse.Models?.Any(l => l.UId == uid) ?? false;
                }
            }
            catch { }

            var vm = new PostsViewModel
            {
                PostId = posts.PostId,
                UId = posts.UId,
                CreatedAt = posts.CreatedAt,
                UpdatedAt = posts.UpdatedAt,
                Title = posts.Title ?? "",
                Content = isSubscribed ? posts.Content : "",
                Status = _enumService.ToEnum<postsStatus>(posts.Status),
                SPId = posts.SPId,
                CoverImageUrl = coverImageUrl,
                IsOwner = isOwner,
                Price = price,
                ArtistProfile = artistProfile,
                IsSubscribed = isSubscribed,
                PlanName = planName,
                Tags = tagNames,
                LikeCount = likeCount,
                IsLiked = isLiked
            };

            return View(vm);
        }

        [Authorize]
        [Route("Posts/add")]
        public IActionResult PostsAddGet()
        {
            return RedirectToAction("Index", "Home");
        }

        [Authorize]
        [Route("Posts/add")]
        [HttpPost]
        public async Task<IActionResult> PostsAdd()
        {
            var redirectResult = RedirectIfNotArtist();
            if (redirectResult != null)
                return redirectResult;

            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidValue))
                return RedirectToAction("Login", "Account");

            var uid = int.Parse(uidValue);

            var client = _supabaseService.GetClient();

            var draftPost = new Posts
            {
                UId = uid,
                Title = "",
                Content = "",
                Status = _enumService.ToStringValue(postsStatus.draft),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            var inserted = await client.From<Posts>().Insert(draftPost);
            var newPost = inserted.Models.FirstOrDefault();

            if (newPost == null)
                return RedirectToAction("Index", "Home");

            return Redirect($"/Posts/{newPost.PostId}/edit");
        }

        [Authorize]
        [Route("Posts/{id:int}/edit")]
        public async Task<IActionResult> PostsEdit(int id)
        {
            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidValue))
                return RedirectToAction("Login", "Account");

            var uid = int.Parse(uidValue);

            var client = _supabaseService.GetClient();
            var post = await client.From<Posts>().Where(p => p.PostId == id && p.UId == uid).Single();

            if (post == null || post.Status != _enumService.ToStringValue(postsStatus.@private) && post.Status != _enumService.ToStringValue(postsStatus.published) && post.Status != _enumService.ToStringValue(postsStatus.draft))
                return View("NotFound");

            var plans = await _supabaseService.GetClient()
            .From<SubscriptionPlan>()
            .Filter("UId", Operator.Equals, uid)
            .Filter("status", Operator.Equals, "active")
            .Order("price", Ordering.Ascending)
            .Get();

            var vm = new PostsAddEditVM
            {
                PostId = post.PostId,
                Title = post.Title ?? "",
                CreatedAt = post.CreatedAt,
                UpdatedAt = post.UpdatedAt,
                Status = _enumService.ToEnum<postsStatus>(post.Status),
                Plans = plans.Models,
                Content = string.IsNullOrWhiteSpace(post.Content)
                ? JsonSerializer.Serialize(new { blocks = new object[] { }, version = "2.26.5" })
                : post.Content
            };

            bool hasPlan = plans.Models.Any(p => p.SPId == post.SPId);

            if (hasPlan)
            {
                vm.SelectedPlanId = post.SPId;
                vm.PublishTarget = postPublishTarget.supporters;
            }
            else
            {
                vm.SelectedPlanId = null;
                vm.PublishTarget = postPublishTarget.all;
            }

            string? imageUrl = null;
            if (!string.IsNullOrEmpty(post.CoverImage))
                imageUrl = _storageService.BuildFileUrl("post_cover", post.CoverImage);

            vm.CoverImageUrl = imageUrl;

            // Load existing tags for this post (tagPost entries where post_id == postId)
            try
            {
                var tagPostResp = await client.From<TagPost>().Filter("post_id", Operator.Equals, post.PostId).Get();
                var tagPosts = tagPostResp.Models?.ToList() ?? new List<TagPost>();
                var tagIds = tagPosts.Select(t => t.TagId).Distinct().ToList();
                if (tagIds.Any())
                {
                    var tagsResp = await client.From<Tag>().Filter("tag_id", Operator.In, tagIds).Get();
                    var tagsFound = tagsResp.Models?.Where(t => !string.IsNullOrEmpty(t.TagName)).ToList() ?? new List<Tag>();
                    vm.SelectedTagIds = string.Join(',', tagIds);
                    vm.SelectedTags = tagsFound.Select(t => t.TagName ?? "").ToList();
                }
            }
            catch { }

            return View("PostsEdit", vm);
        }

        [Authorize]
        [Route("Posts/{id:int}/edit")]
        [HttpPost]
        public async Task<IActionResult> PostsEdit(PostsAddEditVM model)
        {
            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidValue))
                return RedirectToAction("Login", "Account");

            var redirectResult = RedirectIfNotArtist();
            if (redirectResult != null)
                return redirectResult;

            if (!ModelState.IsValid)
                return View(model);

            var uid = int.Parse(uidValue!);

            var client = _supabaseService.GetClient();

            var post = await client.From<Posts>().Where(p => p.PostId == model.PostId && p.UId == uid).Single();
            if (post == null || post.Status == _enumService.ToStringValue(postsStatus.deleted))
                return View("NotFound");

            post.Title = model.Title;
            post.Content = model.Content;
            post.UpdatedAt = DateTime.UtcNow;
            post.Status = _enumService.ToStringValue(postsStatus.published);

            if (model.PublishTarget == postPublishTarget.all)
            {
                post.SPId = null;
            }
            else if (model.PublishTarget == postPublishTarget.supporters)
            {
                post.SPId = model.SelectedPlanId;
            }

            if (model.CoverImage != null && model.CoverImage.Length > 0)
            {
                if (!string.IsNullOrEmpty(post.CoverImage))
                {
                    _storageService.Delete("post_cover", post.CoverImage);
                }

                var newFileName = await _storageService.SaveAsync(model.CoverImage, "post_cover", new[] { "image/jpeg", "image/png" });
                post.CoverImage = newFileName ?? "";
            }
            else if (model.RemoveCoverImage)
            {
                if (!string.IsNullOrEmpty(post.CoverImage))
                    _storageService.Delete("poat_cover", post.CoverImage);

                post.CoverImage = "";
            }

            await client.From<Posts>().Update(post);

            // Save selected tags for the post using tagPost table (post_id = postId). Limit to 10 tags.
            try
            {
                var selectedTagString = Request.Form["SelectedTagIds"].FirstOrDefault() ?? string.Empty;
                var selectedIds = selectedTagString.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => { if (int.TryParse(s, out var id)) return id; return -1; }).Where(i => i > 0).Distinct().Take(10).ToList();

                // Remove existing tagPost entries for this post
                var existing = await client.From<TagPost>().Filter("post_id", Operator.Equals, post.PostId).Get();
                foreach (var ea in existing.Models)
                {
                    try { await client.From<TagPost>().Where(x => x.TPId == ea.TPId).Delete(); } catch { }
                }

                // Validate that tag ids correspond to existing tags before inserting
                if (selectedIds.Any())
                {
                    var tagsResp = await client.From<Tag>().Filter("tag_id", Operator.In, selectedIds.ToList()).Get();
                    var validTagIds = tagsResp.Models?.Where(t => !string.IsNullOrEmpty(t.TagName)).Select(t => t.TagId).ToHashSet() ?? new HashSet<int>();

                    foreach (var tId in selectedIds)
                    {
                        if (!validTagIds.Contains(tId)) continue; // skip invalid ids
                        var tagPost = new TagPost { CreatedAt = DateTime.UtcNow, PostId = post.PostId, TagId = tId };
                        await client.From<TagPost>().Insert(tagPost);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to save tags for post: " + ex.Message);
            }

            var unlinkedFiles = await client
            .From<PostFiles>()
            .Filter("UId", Operator.Equals, uid)
            .Filter("post_id", Operator.Equals, post.PostId)
            .Filter("is_linked", Operator.Equals, "false")
            .Get();

            foreach (var file in unlinkedFiles.Models)
            {
                file.IsLinked = true;
                await client.From<PostFiles>().Update(file);
            }

            TempData["SuccessMessage"] = "Post added";
            return Redirect($"/Posts/{model.PostId}");
        }

        [HttpGet]
        [Route("/Posts/FetchUrlMetadata")]
        public async Task<IActionResult> FetchUrlMetadata([FromQuery] string url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return BadRequest();

            try
            {
                var domain = new Uri(url).Host.ToLower();

                using var httpClient = new HttpClient();
                var apiUrl = $"https://noembed.com/embed?url={url}";
                var json = await httpClient.GetStringAsync(apiUrl);
                var data = System.Text.Json.JsonDocument.Parse(json).RootElement;

                var title = data.TryGetProperty("title", out var t) ? t.GetString() : url;
                var thumbnail = data.TryGetProperty("thumbnail_url", out var thumb) ? thumb.GetString() : null;

                return Json(new
                {
                    success = 1,
                    meta = new
                    {
                        title,
                        image = new { url = thumbnail ?? $"https://www.google.com/s2/favicons?domain={domain}" }
                    }
                });
            }
            catch
            {
                var domain = new Uri(url).Host;
                return Json(new
                {
                    success = 1,
                    meta = new
                    {
                        title = domain,
                        image = new { url = $"https://www.google.com/s2/favicons?domain={domain}" }
                    }
                });
            }
        }

        public class LinkRequest
        {
            public string url { get; set; }
        }

        [HttpPost]
        [Route("Posts/UploadImage")]
        [Authorize]
        public async Task<IActionResult> UploadImage(IFormFile image, [FromForm] int postId)
        {
            if (image == null || image.Length == 0)
                return BadRequest(new { success = 0, message = "No file uploaded." });

            try
            {
                var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(uidValue))
                    return Unauthorized(new { success = 0, message = "User not logged in." });

                var uid = int.Parse(uidValue);
                var client = _supabaseService.GetClient();

                var fileName = await _storageService.SaveAsync(
                    image,
                    "post",
                    new[] { "image/jpeg", "image/png", "image/gif" }
                );

                var fileUrl = _storageService.BuildFileUrl("post", fileName!);

                var postFile = new PostFiles
                {
                    CreatedAt = DateTime.UtcNow,
                    FileName = fileName!,
                    Type = _enumService.ToStringValue(postFilesType.image),
                    UId = uid,
                    IsLinked = false,
                    PostId = postId
                };

                await client.From<PostFiles>().Insert(postFile);

                return Json(new
                {
                    success = 1,
                    file = new
                    {
                        url = fileUrl
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Image upload failed: {ex.Message}");
                return BadRequest(new { success = 0, message = "Upload failed." });
            }
        }

        [HttpPost]
        [Route("Posts/UploadFile")]
        [Authorize]
        public async Task<IActionResult> UploadFile(IFormFile file, [FromForm] int postId)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { success = 0, message = "No file uploaded." });

            try
            {
                var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(uidValue))
                    return Unauthorized(new { success = 0, message = "User not logged in." });

                var uid = int.Parse(uidValue);
                var client = _supabaseService.GetClient();

                var fileName = await _storageService.SaveAsync(
                    file,
                    "post",
                    LocalStorageService.DefaultAllowedContentTypes
                );

                if (fileName == null)
                    return BadRequest(new { success = 0, message = "Invalid file type." });

                var fileUrl = _storageService.BuildFileUrl("post", fileName);

                long fileSizeBytes = file.Length;
                string fileSize = fileSizeBytes switch
                {
                    < 1024 => $"{fileSizeBytes} B",
                    < 1048576 => $"{fileSizeBytes / 1024.0:F1} KB",
                    _ => $"{fileSizeBytes / 1048576.0:F1} MB"
                };

                var postFile = new PostFiles
                {
                    CreatedAt = DateTime.UtcNow,
                    FileName = fileName,
                    Type = _enumService.ToStringValue(postFilesType.file),
                    IsLinked = false,
                    UId = uid,
                    PostId = postId
                };

                await client.From<PostFiles>().Insert(postFile);

                return Json(new
                {
                    success = 1,
                    file = new
                    {
                        url = fileUrl,
                        name = file.FileName,
                        title = file.FileName,
                        size = file.Length
                    }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"File upload failed: {ex.Message}");
                return BadRequest(new { success = 0, message = "Upload failed." });
            }
        }

        [HttpPost]
        [Route("Posts/DeleteFile")]
        [Authorize]
        public async Task<IActionResult> DeleteFile([FromBody] DeleteFileRequest request)
        {
            if (string.IsNullOrEmpty(request.FileName))
                return BadRequest(new { success = 0, message = "Invalid file name." });

            try
            {
                var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (string.IsNullOrEmpty(uidValue))
                    return Unauthorized(new { success = 0, message = "User not logged in." });

                var uid = int.Parse(uidValue);
                var client = _supabaseService.GetClient();

                var fileRecord = await client
                    .From<PostFiles>()
                    .Filter("UId", Operator.Equals, uid)
                    .Filter("file_name", Operator.Equals, request.FileName)
                    .Single();

                if (fileRecord == null)
                    return NotFound(new { success = 0, message = "File not found." });

                _storageService.Delete("post", fileRecord.FileName);
                await client.From<PostFiles>().Delete(fileRecord);

                return Json(new { success = 1 });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"File delete failed: {ex.Message}");
                return BadRequest(new { success = 0, message = "File deletion failed." });
            }
        }

        public class DeleteFileRequest
        {
            public string FileName { get; set; } = string.Empty;
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> PostsDelete(int id)
        {
            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidValue))
                return RedirectToAction("Login", "Account");

            var redirect = RedirectIfNotArtist();
            if (redirect != null)
                return redirect;

            var uid = int.Parse(uidValue!);

            var existingPost = await _supabaseService.GetClient()
            .From<Posts>()
            .Filter("post_id", Operator.Equals, id)
            .Filter("UId", Operator.Equals, uid)
            .Filter("status", Operator.NotEqual, _enumService.ToStringValue(postsStatus.deleted))
            .Single();

            if (existingPost == null)
                return Redirect($"/Profile/{uidValue}/posts");

            var response = await _supabaseService.GetClient()
            .From<Posts>()
            .Where(p => p.PostId == id && p.UId == uid)
            .Set(p => new KeyValuePair<object, object?>(p.Status, _enumService.ToStringValue(postsStatus.deleted)))
            .Set(p => new KeyValuePair<object, object?>(p.UpdatedAt, DateTime.UtcNow))
            .Update();

            if (response.Models.Count == 0)
                return Redirect($"/Profile/{uidValue}/posts");
            else
            {
                if (!string.IsNullOrEmpty(existingPost.CoverImage))
                    _storageService.Delete("post_cover", existingPost.CoverImage);

                var postFiles = await _supabaseService.GetClient()
                .From<PostFiles>()
                .Filter("post_id", Operator.Equals, id)
                .Filter("UId", Operator.Equals, uid)
                .Get();

                if (postFiles != null && postFiles.Models.Count > 0)
                {
                    foreach (var file in postFiles.Models)
                    {
                        try
                        {
                            _storageService.Delete("post", file.FileName);
                            await _supabaseService.GetClient()
                                .From<PostFiles>()
                                .Where(f => f.PostId == file.PostId)
                                .Delete();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Failed to delete file {file.FileName}: {ex.Message}");
                        }
                    }
                }
            }

            TempData["SuccessMessage"] = "Post deleted";
            return Redirect($"/Posts/manage");
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> PostsVisiToggle(int id)
        {
            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidValue))
                return RedirectToAction("Login", "Account");

            var redirect = RedirectIfNotArtist();
            if (redirect != null)
                return redirect;

            var uid = int.Parse(uidValue!);

            var existingPost = await _supabaseService.GetClient()
            .From<Posts>()
            .Filter("post_id", Operator.Equals, id)
            .Filter("UId", Operator.Equals, uid)
            .Filter("status", Operator.NotEqual, _enumService.ToStringValue(postsStatus.deleted))
            .Filter("status", Operator.NotEqual, _enumService.ToStringValue(postsStatus.draft))
            .Single();

            if (existingPost == null)
                return Redirect($"/Profile/{uidValue}/posts");

            // Toggle status: published <-> private
            string newStatus = existingPost.Status switch
            {
                var s when s == _enumService.ToStringValue(postsStatus.published) => _enumService.ToStringValue(postsStatus.@private),
                var s when s == _enumService.ToStringValue(postsStatus.@private) => _enumService.ToStringValue(postsStatus.published),
                _ => existingPost.Status
            };

            var response = await _supabaseService.GetClient()
            .From<Posts>()
            .Where(p => p.PostId == id && p.UId == uid)
            .Set(p => new KeyValuePair<object, object?>(p.Status, newStatus))
            .Set(p => new KeyValuePair<object, object?>(p.UpdatedAt, DateTime.UtcNow))
            .Update();

            if (response.Models.Count == 0)
                return Redirect($"/Profile/{uidValue}/posts");

            TempData["SuccessMessage"] = "Post visibility updated";
            return Redirect($"/Posts/manage");
        }

        [HttpGet]
        [Route("Posts/SuggestTags")]
        public async Task<IActionResult> SuggestTags([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
                return Json(new { tags = new string[] { } });

            try
            {
                var client = _supabaseService.GetClient();
                
                // Remove # prefix if present
                var searchTerm = query.TrimStart('#');
                
                // Get available tags that match the search term
                var tagsResponse = await client.From<Tag>()
                    .Filter("status", Operator.Equals, _enumService.ToStringValue(tagStatus.available))
                    .Get();
                
                var matchingTags = tagsResponse.Models?
                    .Where(t => !string.IsNullOrEmpty(t.TagName) && 
                               t.TagName.Contains(searchTerm, StringComparison.OrdinalIgnoreCase))
                    .Select(t => t.TagName!)
                    .Take(10)
                    .ToList() ?? new List<string>();

                return Json(new { tags = matchingTags });
            }
            catch (Exception ex)
            {
                Console.WriteLine("Failed to suggest tags: " + ex.Message);
                return Json(new { tags = new string[] { } });
            }
        }

        /// <summary>
        /// Get comments for a post with pagination
        /// </summary>
        [HttpGet]
        [Route("Posts/{postId:int}/comments")]
        [AllowAnonymous]
        public async Task<IActionResult> GetComments(int postId, int page = 1, int pageSize = 10)
        {
            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0";
            var uid = int.Parse(uidValue);
            var client = _supabaseService.GetClient();

            // Get the post to check ownership and subscription requirements
            var post = await client.From<Posts>().Where(p => p.PostId == postId).Single();
            if (post == null)
                return NotFound(new { message = "Post not found" });

            bool isOwner = post.UId == uid;
            bool requiresSubscription = post.SPId != null;
            bool isSubscribed = await IsUserSubscribedToPostAsync(uid, post);
            bool canViewComments = isSubscribed; // Only subscribers can view comments on paid posts

            // If user cannot view comments, return empty list with appropriate flags
            if (!canViewComments)
            {
                return Json(new PostCommentsListViewModel
                {
                    Comments = new List<PostCommentViewModel>(),
                    TotalCount = 0,
                    CurrentPage = 1,
                    TotalPages = 0,
                    PageSize = pageSize,
                    CanComment = false,
                    IsAuthenticated = uid != 0,
                    RequiresSubscription = true,
                    CanViewComments = false
                });
            }

            // Get comments for this post
            var commentsResponse = await client
                .From<PostComment>()
                .Filter("post_id", Operator.Equals, postId)
                .Filter("status", Operator.Equals, _enumService.ToStringValue(postCommentStatus.active))
                .Order("created_at", Ordering.Descending)
                .Get();

            var allComments = commentsResponse.Models?.ToList() ?? new List<PostComment>();
            
            // Separate main comments (parent_id is null) and replies
            var mainComments = allComments.Where(c => c.ParentId == null).ToList();
            var replies = allComments.Where(c => c.ParentId != null).ToList();
            
            int totalCount = mainComments.Count;
            int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            if (page > totalPages && totalPages > 0)
                page = totalPages;

            var paginatedMainComments = mainComments
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // Get user info for all comments (main and replies for paginated main comments)
            var paginatedMainCommentIds = paginatedMainComments.Select(c => c.CommentId).ToHashSet();
            var relevantReplies = replies.Where(r => r.ParentId.HasValue && paginatedMainCommentIds.Contains(r.ParentId.Value)).ToList();
            
            var allRelevantComments = paginatedMainComments.Concat(relevantReplies).ToList();
            var userIds = allRelevantComments.Select(c => c.UId).Distinct().ToList();
            var usersDict = new Dictionary<int, Users>();
            if (userIds.Any())
            {
                var usersResponse = await client.From<Users>().Filter("UId", Operator.In, userIds).Get();
                usersDict = usersResponse.Models?.ToDictionary(u => u.Uid, u => u) ?? new Dictionary<int, Users>();
            }

            // Helper function to convert PostComment to PostCommentViewModel
            PostCommentViewModel ToViewModel(PostComment c)
            {
                usersDict.TryGetValue(c.UId, out var user);
                string? profilePicUrl = null;
                if (user != null && !string.IsNullOrEmpty(user.ProfilePic))
                    profilePicUrl = _storageService.BuildFileUrl("profile_pic", user.ProfilePic);

                return new PostCommentViewModel
                {
                    CommentId = c.CommentId,
                    PostId = c.PostId,
                    UserId = c.UId,
                    UserName = user?.Username ?? "Unknown",
                    UserNickname = user?.Nickname ?? user?.Username ?? "Unknown",
                    UserProfilePicUrl = profilePicUrl,
                    CreatedAt = c.CreatedAt,
                    Comment = c.Comment,
                    CanDelete = isOwner || c.UId == uid,
                    ParentId = c.ParentId
                };
            }

            // Build hierarchical structure: main comments with their replies
            var commentViewModels = paginatedMainComments.Select(c =>
            {
                var vm = ToViewModel(c);
                // Get replies for this comment and sort by created_at ascending (oldest first)
                vm.Replies = relevantReplies
                    .Where(r => r.ParentId == c.CommentId)
                    .OrderBy(r => r.CreatedAt)
                    .Select(ToViewModel)
                    .ToList();
                return vm;
            }).ToList();

            var result = new PostCommentsListViewModel
            {
                Comments = commentViewModels,
                TotalCount = totalCount,
                CurrentPage = page,
                TotalPages = totalPages,
                PageSize = pageSize,
                CanComment = isSubscribed && uid != 0,
                IsAuthenticated = uid != 0,
                RequiresSubscription = requiresSubscription && !isSubscribed,
                CanViewComments = true
            };

            return Json(result);
        }

        /// <summary>
        /// Add a comment to a post
        /// </summary>
        [HttpPost]
        [Route("Posts/{postId:int}/comments")]
        [Authorize]
        public async Task<IActionResult> AddComment(int postId, [FromBody] AddCommentRequest request)
        {
            if (string.IsNullOrWhiteSpace(request?.Comment))
                return BadRequest(new { message = "Comment cannot be empty" });

            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidValue))
                return Unauthorized(new { message = "User not logged in" });

            var uid = int.Parse(uidValue);
            var client = _supabaseService.GetClient();

            // Get the post
            var post = await client.From<Posts>().Where(p => p.PostId == postId).Single();
            if (post == null)
                return NotFound(new { message = "Post not found" });

            // Check subscription status
            bool isSubscribed = await IsUserSubscribedToPostAsync(uid, post);
            if (!isSubscribed)
                return Forbid();

            // Store comment as plain text (no encoding - will be displayed as textContent in UI)
            var commentText = request.Comment.Trim();
            if (commentText.Length > 1000)
                commentText = commentText.Substring(0, 1000);

            var newComment = new PostComment
            {
                PostId = postId,
                UId = uid,
                Comment = commentText,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                Status = _enumService.ToStringValue(postCommentStatus.active),
                ParentId = request.ParentId
            };

            var inserted = await client.From<PostComment>().Insert(newComment);
            var comment = inserted.Models.FirstOrDefault();

            if (comment == null)
                return BadRequest(new { message = "Failed to add comment" });

            // Get user info for response
            var user = await client.From<Users>().Where(u => u.Uid == uid).Single();
            string? profilePicUrl = null;
            if (user != null && !string.IsNullOrEmpty(user.ProfilePic))
                profilePicUrl = _storageService.BuildFileUrl("profile_pic", user.ProfilePic);

            var result = new PostCommentViewModel
            {
                CommentId = comment.CommentId,
                PostId = comment.PostId,
                UserId = comment.UId,
                UserName = user?.Username ?? "Unknown",
                UserNickname = user?.Nickname ?? user?.Username ?? "Unknown",
                UserProfilePicUrl = profilePicUrl,
                CreatedAt = comment.CreatedAt,
                Comment = comment.Comment,
                CanDelete = true,
                ParentId = comment.ParentId
            };

            return Json(result);
        }

        /// <summary>
        /// Delete a comment
        /// </summary>
        [HttpDelete]
        [Route("Posts/{postId:int}/comments/{commentId:int}")]
        [Authorize]
        public async Task<IActionResult> DeleteComment(int postId, int commentId)
        {
            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidValue))
                return Unauthorized(new { message = "User not logged in" });

            var uid = int.Parse(uidValue);
            var client = _supabaseService.GetClient();

            // Get the post
            var post = await client.From<Posts>().Where(p => p.PostId == postId).Single();
            if (post == null)
                return NotFound(new { message = "Post not found" });

            // Get the comment
            var comment = await client.From<PostComment>()
                .Filter("comment_id", Operator.Equals, commentId)
                .Filter("post_id", Operator.Equals, postId)
                .Single();

            if (comment == null)
                return NotFound(new { message = "Comment not found" });

            bool isPostOwner = post.UId == uid;
            bool isCommentOwner = comment.UId == uid;

            // Only post owner or comment owner can delete
            if (!isPostOwner && !isCommentOwner)
                return Forbid();

            // Soft delete the comment
            comment.Status = _enumService.ToStringValue(postCommentStatus.deleted);
            comment.UpdatedAt = DateTime.UtcNow;
            await client.From<PostComment>().Update(comment);

            return Json(new { success = true, message = "Comment deleted" });
        }

        public class AddCommentRequest
        {
            public string Comment { get; set; } = string.Empty;
            public int? ParentId { get; set; }
        }

        /// <summary>
        /// Toggle like for a post (like/unlike)
        /// </summary>
        [HttpPost]
        [Route("Posts/{postId:int}/like")]
        [Authorize]
        public async Task<IActionResult> ToggleLike(int postId)
        {
            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidValue) || !int.TryParse(uidValue, out var uid))
                return Unauthorized(new { success = false, message = "User not logged in" });

            var client = _supabaseService.GetClient();

            // Check if the post exists
            var post = await client.From<Posts>().Where(p => p.PostId == postId).Single();
            if (post == null || (post.Status != _enumService.ToStringValue(postsStatus.published) && post.Status != _enumService.ToStringValue(postsStatus.@private)))
                return NotFound(new { success = false, message = "Post not found" });

            // Check if user already liked the post
            var existingLike = await client
                .From<PostLike>()
                .Filter("post_id", Operator.Equals, postId)
                .Filter("UId", Operator.Equals, uid)
                .Single();

            bool isLiked;
            if (existingLike != null)
            {
                // Unlike - remove the like
                await client.From<PostLike>().Where(l => l.PLId == existingLike.PLId).Delete();
                isLiked = false;
            }
            else
            {
                // Like - add the like
                var newLike = new PostLike
                {
                    PostId = postId,
                    UId = uid,
                    CreatedAt = DateTime.UtcNow
                };
                await client.From<PostLike>().Insert(newLike);
                isLiked = true;
            }

            // Get updated like count
            var likesResponse = await client.From<PostLike>().Filter("post_id", Operator.Equals, postId).Get();
            int likeCount = likesResponse.Models?.Count ?? 0;

            return Json(new { success = true, isLiked, likeCount });
        }

        /// <summary>
        /// Helper method to check if a user is subscribed to a post's subscription plan
        /// </summary>
        private async Task<bool> IsUserSubscribedToPostAsync(int uid, Posts post)
        {
            if (post.UId == uid)
                return true;  // Owner is always subscribed

            if (post.SPId == null)
                return true;  // No subscription required

            var client = _supabaseService.GetClient();

            // Get user's active subscriptions
            var subscriptionsTask = await client
                .From<Subscription>()
                .Filter("UId", Operator.Equals, uid)
                .Filter("end_date", Operator.GreaterThan, DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.ffffff+00"))
                .Filter("status", Operator.Equals, _enumService.ToStringValue(subscriptionStatus.active))
                .Filter("payment_status", Operator.Equals, _enumService.ToStringValue(paymentStatus.paid))
                .Get();

            var subscriptions = subscriptionsTask.Models.ToList();
            if (!subscriptions.Any())
                return false;

            // Get the post's subscription plan
            var postPlan = await client.From<SubscriptionPlan>().Where(s => s.SPId == post.SPId).Single();
            if (postPlan == null)
                return false;

            // Get all subscription plan IDs at once
            var subscriptionPlanIds = subscriptions.Select(s => s.SPId).Distinct().ToList();
            
            // Fetch all user's subscription plans in one query
            var userPlansResponse = await client
                .From<SubscriptionPlan>()
                .Filter("SPId", Operator.In, subscriptionPlanIds)
                .Filter("status", Operator.Equals, _enumService.ToStringValue(subscriptionPlanStatus.active))
                .Get();

            var userPlans = userPlansResponse.Models?.ToList() ?? new List<SubscriptionPlan>();

            // Check if any of the user's plans qualifies for this post
            return userPlans.Any(plan => plan.UId == post.UId && plan.Price >= postPlan.Price);
        }

        private void SetCacheHeaders()
        {
            var isAuthenticated = User?.Identity?.IsAuthenticated ?? false;

            if (isAuthenticated)
            {
                // Authenticated users: no caching (they see personalized content)
                Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
                Response.Headers["Pragma"] = "no-cache";
                Response.Headers["Expires"] = "0";
            }
            else
            {
                // Public posts: cache for 5 minutes
                Response.Headers["Cache-Control"] = "public, max-age=300";
                Response.Headers["Vary"] = "Accept-Encoding";
            }
        }

        private IActionResult? RedirectIfNotArtist()
        {
            var user = User;
            var identity = user?.Identity;

            if (!user.IsInRole("artist"))
                return RedirectToAction("Index", "Home");

            return null;
        }
    }
}
