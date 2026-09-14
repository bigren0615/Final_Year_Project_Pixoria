using Final_Year_Project.Enums;
using Final_Year_Project.Models.Address;
using Final_Year_Project.Models.CommissionPlans;
using Final_Year_Project.Models.DB;
using Final_Year_Project.Models.Profile;
using Final_Year_Project.Models.Sales;
using Final_Year_Project.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Supabase;
using Supabase.Postgrest.Responses;
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reactive.Joins;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Cryptography;
using System.Threading.Tasks;
using static Supabase.Postgrest.Constants;

namespace Final_Year_Project.Controllers
{
    [AllowAnonymous]
    public class ProfileController : Controller
    {
        private readonly SupabaseService _supabaseService;
        private readonly LocalStorageService _storageService;
        private readonly EnumService _enumService;

        public ProfileController(SupabaseService supabaseService, LocalStorageService storageService, EnumService enumService)
        {
            _supabaseService = supabaseService;
            _storageService = storageService;
            _enumService = enumService;
        }

        [Route("Profile/{id:int}")]
        public async Task<IActionResult> Profile(int id)
        {
            SetCacheHeaders();

            var model = await BuildProfileViewModelAsync(id);
            if (model == null)
                return View("NotFound");

            var path = HttpContext.Request.Path.Value?.ToLower();
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
                Response.Headers["Vary"] = "X-Requested-With";

                if (path!.Contains("/posts"))
                    return PartialView("_PostsPartial", model);
                else if (path.Contains("/plans"))
                    return PartialView("_PlansPartial", model);
                else if (path.Contains("/products"))
                    return PartialView("_ProductsPartial", model);
                else
                    return PartialView("_HomePartial", model);
            }

            return View("ProfileLayout", model);
        }

        [Route("Profile/{id:int}/posts")]
        public async Task<IActionResult> Posts(int id, int page = 1, int pageSize = 12)
        {
            var redirectResult = RedirectIfNotArtist(id);
            if (redirectResult != null)
                return redirectResult;

            SetCacheHeaders();

            var model = await BuildProfileViewModelAsync(id);
            if (model == null) return View("NotFound");

            var client = _supabaseService.GetClient();

            var response = await client
                .From<Posts>()
                .Filter("UId", Operator.Equals, id)
                .Filter("status", Operator.Equals, _enumService.ToStringValue(postsStatus.published))
                .Order("created_at", Ordering.Descending)
                .Range((page - 1) * pageSize, (page * pageSize) + 1)
                .Get();

            var postsData = response.Models.Take(pageSize).ToList();
            var hasNextPage = response.Models.Count > pageSize;

            var planIds = postsData
                .Where(p => p.SPId.HasValue)
                .Select(p => (int)p.SPId!)
                .Distinct()
                .ToList();

            var planPrices = new Dictionary<int, decimal>();
            var planTitle = new Dictionary<int, string>();
            if (planIds.Any())
            {
                var plansResponse = await client
                    .From<SubscriptionPlan>()
                    .Filter("SPId", Operator.In, planIds)
                    .Get();

                planTitle = plansResponse.Models.ToDictionary(p => p.SPId, p => p.Title);
                planPrices = plansResponse.Models.ToDictionary(p => p.SPId, p => p.Price);
            }

            var posts = postsData.Select(p => new PostViewModel
            {
                PostId = p.PostId,
                Title = p.Title ?? "",
                Content = p.Content,
                CreatedAt = p.CreatedAt,
                UpdatedAt = p.UpdatedAt,
                Status = p.Status,
                SPId = p.SPId,
                CoverImageUrl = !string.IsNullOrEmpty(p.CoverImage)
                    ? _storageService.BuildFileUrl("post_cover", p.CoverImage)
                    : null,
                Price = p.SPId.HasValue && planPrices.TryGetValue(p.SPId.Value, out var price)
                    ? price
                    : null,
                PlanTitle = p.SPId.HasValue && planTitle.TryGetValue(p.SPId.Value, out var title)
                    ? title
                    : null
            }).ToList();

            model.Posts = posts;
            ViewData["CurrentPage"] = page;
            ViewData["HasNextPage"] = hasNextPage;
            ViewData["PageSize"] = pageSize;

            var totalResponse = await client
                .From<Posts>()
                .Filter("UId", Operator.Equals, id)
                .Filter("status", Operator.Equals, _enumService.ToStringValue(postsStatus.published))
                .Get();

            int totalCount = totalResponse?.Models?.Count ?? posts.Count;
            int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            ViewData["TotalPages"] = Math.Max(1, totalPages);

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                Response.Headers["Cache-Control"] = "public, max-age=60";
                Response.Headers["Vary"] = "X-Requested-With";
                return PartialView("_PostsPartial", model);
            }

            ViewData["InitialTab"] = "posts";
            return View("ProfileLayout", model);
        }

        [Route("Profile/{id:int}/plans")]
        public async Task<IActionResult> Plans(int id, int page = 1, int pageSize = 6)
        {
            var redirectResult = RedirectIfNotArtist(id);
            if (redirectResult != null)
                return redirectResult;
            int uid = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)! ?? "0");

            SetCacheHeaders();

            var model = await BuildProfileViewModelAsync(id);
            if (model == null) return View("NotFound");

            var client = _supabaseService.GetClient();

            var response = await client
                .From<SubscriptionPlan>()
                .Where(p => p.UId == id && p.Status == "active")
                .Order("price", Ordering.Ascending)
                .Range(0, 7)
                .Get();

            var plansData = response.Models.ToList();

            var tomorrow = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.ffffff+00");
            var planTasks = plansData.Select(async p =>
            {
                string? img = !string.IsNullOrEmpty(p.Image)
                    ? _storageService.BuildFileUrl("subscriptionPlan_cover", p.Image)
                    : null;

                bool subscribed = false;

                var subscribeResponse = await client
                    .From<Subscription>()
                    .Filter("UId", Operator.Equals, uid)
                    .Filter("SPId", Operator.Equals, p.SPId)
                    .Filter("end_date", Operator.GreaterThan, tomorrow)
                    .Filter("status", Operator.Equals, _enumService.ToStringValue(subscriptionStatus.active))
                    .Filter("payment_status", Operator.Equals, _enumService.ToStringValue(paymentStatus.paid))
                    .Single();

                if (subscribeResponse != null)
                    subscribed = true;


                return new SubscriptionPlanViewModel
                {
                    SPId = p.SPId,
                    Title = p.Title ?? "",
                    Description = p.Description,
                    Price = p.Price,
                    ImageUrl = img,
                    Subscribed = subscribed
                };
            }).ToList();

            var plans = (await Task.WhenAll(planTasks)).ToList();

            model.SubscriptionPlans = plans;

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                Response.Headers["Cache-Control"] = "public, max-age=60";
                Response.Headers["Vary"] = "X-Requested-With";
                return PartialView("_PlansPartial", model);
            }

            ViewData["InitialTab"] = "plans";
            return View("ProfileLayout", model);
        }

        [Route("Profile/{id:int}/commissions")]
        public async Task<IActionResult> Commissions(int id)
        {
            var redirectResult = RedirectIfNotArtist(id);
            if (redirectResult != null)
                return redirectResult;

            SetCacheHeaders();

            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0";
            var uid = int.Parse(uidValue);
            var model = await BuildProfileViewModelAsync(id);
            if (model == null) return View("NotFound");

            var client = _supabaseService.GetClient();

            if (uid == id)
            {
                var response = await client
                    .From<CommissionPlan>()
                    .Where(p => p.UId == id)
                    .Filter("status", Operator.In, new[] { _enumService.ToStringValue(commissionPlanStatus.available), _enumService.ToStringValue(commissionPlanStatus.@private) })
                    .Order("target_price", Ordering.Ascending)
                    .Get();

                var commissionTasks = response.Models.Select(async p =>
                {
                    string? imageUrl = null;
                    if (!string.IsNullOrEmpty(p.Image))
                        imageUrl = _storageService.BuildFileUrl("commissionPlan_cover", p.Image);
                    return new CommissionPlanViewModel
                    {
                        CId = p.CId,
                        Title = p.Title,
                        Description = p.Description,
                        Category = _enumService.ToEnum<commissionPlanCategory>(p.Category),
                        TargetPrice = p.TargetPrice,
                        Status = _enumService.ToEnum<commissionPlanStatus>(p.Status),
                        ImageUrl = imageUrl
                    };
                }).ToList();

                var commissions = (await Task.WhenAll(commissionTasks)).ToList();

                model.CommissionPlans = commissions;
            }
            else
            {
                var status = _enumService.ToStringValue(commissionPlanStatus.available);
                var response = await client
                     .From<CommissionPlan>()
                     .Where(p => p.UId == id && p.Status == status)
                     .Order("target_price", Ordering.Ascending)
                     .Get();

                var commissionTasks = response.Models.Select(async p =>
                {
                    string? imageUrl = null;
                    if (!string.IsNullOrEmpty(p.Image))
                        imageUrl = _storageService.BuildFileUrl("commissionPlan_cover", p.Image);
                    return new CommissionPlanViewModel
                    {
                        CId = p.CId,
                        Title = p.Title,
                        Description = p.Description,
                        Category = _enumService.ToEnum<commissionPlanCategory>(p.Category),
                        TargetPrice = p.TargetPrice,
                        ImageUrl = imageUrl
                    };
                }).ToList();

                var commissions = (await Task.WhenAll(commissionTasks)).ToList();

                model.CommissionPlans = commissions;
            }

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
                Response.Headers["Vary"] = "X-Requested-With";
                return PartialView("_CommissionsPartial", model);
            }

            ViewData["InitialTab"] = "commissions";
            return View("ProfileLayout", model);
        }

        [Route("Profile/{id:int}/products")]
        public async Task<IActionResult> Products(int id)
        {
            var redirectResult = RedirectIfNotArtist(id);
            if (redirectResult != null)
                return redirectResult;

            SetCacheHeaders();

            var model = await BuildProfileViewModelAsync(id);
            if (model == null) return View("NotFound");

            var client = _supabaseService.GetClient();

            var response = await client
            .From<Product>()
            .Where(p => p.Uid == id && p.Status == "available")
            .Order("price", Ordering.Ascending)
            .Range(0, 8)
            .Get();

            var productsData = response.Models.Take(8).ToList();

            // Get primary images for products
            var productIds = productsData.Select(p => p.PId).Distinct().ToList();
            var primaryImages = new Dictionary<string, string>();

            if (productIds.Any())
            {
                var imagesResponse = await client
                    .From<ProductImage>()
                    .Filter("is_primary", Operator.Equals, "true")
                    .Filter("PId", Operator.In, productIds)
                    .Get();

                foreach (var img in imagesResponse.Models)
                {
                    if (!primaryImages.ContainsKey(img.PId ?? ""))
                        primaryImages[img.PId ?? ""] = img.ImagePath ?? "";
                }
            }

            var products = productsData.Select(p => new ArtistProductDetailsViewModel
            {
                PId = p.PId,
                ProductName = p.ProductName ?? "",
                Description = p.Description,
                Price = p.Price,
                Status = p.Status,
                StockAvailable = p.StockAvailable,
                PrimaryImage = primaryImages.ContainsKey(p.PId ?? "") ? primaryImages[p.PId ?? ""] : null,
                CreatedAt = p.CreatedDate
            }).ToList();

            model.Products = products;

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
                Response.Headers["Vary"] = "X-Requested-With";
                return PartialView("_ProductsPartial", model);
            }

            ViewData["InitialTab"] = "products";
            return View("ProfileLayout", model);
        }

        [Route("Profile/{id:int}/edit")]
        [Authorize]
        public async Task<IActionResult> ProfileEdit(int id)
        {
            var redirectResult = RedirectIfNotArtist(id);
            if (redirectResult != null)

                return redirectResult;
            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidValue))
                return RedirectToAction("Login", "Account");

            var uid = int.Parse(uidValue!);

            var user = await _supabaseService.GetClient()
                                .From<Users>()
                                .Filter("UId", Operator.Equals, uid)
                                .Single();
            if (user == null || user.Status != "active" || user.Uid != uid || user.Uid != id)
                return View("NotFound");

            var artworkResponse = await _supabaseService.GetClient()
                                        .From<ProfileArtwork>()
                                        .Filter("UId", Operator.Equals, uid)
                                        .Order("created_at", Ordering.Ascending)
                                        .Get();

            var artworkTasks = artworkResponse.Models.Select(art =>
            {
                string? imageUrl = string.IsNullOrEmpty(art.Image)
                    ? null
                    : _storageService.BuildFileUrl("profile_artwork", art.Image);

                return Task.FromResult(new ProfileArtworkAddEditViewModel
                {
                    ProfArtid = art.ProfArtid,
                    ImageUrl = imageUrl
                });
            }).ToList();

            var artworks = (await Task.WhenAll(artworkTasks)).ToList();

            var vm = new ProfileEditViewModel
            {
                UserId = user.Uid,
                Nickname = user.Nickname,
                DOB = user.DOB,
                Gender = user.Gender,
                AboutMe = user.AboutMe,
                Artworks = artworks
            };

            string? imageUrl = null;
            if (!string.IsNullOrEmpty(user.ProfilePic))
                imageUrl = _storageService.BuildFileUrl("profile_pic", user.ProfilePic);

            vm.ProfilePicUrl = imageUrl;

            string? bannerUrl = null;
            if (!string.IsNullOrEmpty(user.BannerImage))
                bannerUrl = _storageService.BuildFileUrl("banner_image", user.BannerImage);

            vm.BannerImageUrl = bannerUrl;

            return View(vm);
        }

        [Route("Profile/{id:int}/edit")]
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> ProfileEdit(int id, ProfileEditViewModel model)
        {
            var redirectResult = RedirectIfNotArtist(id);
            if (redirectResult != null)
                return redirectResult;

            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidValue)) return RedirectToAction("Login", "Account");

            var uid = int.Parse(uidValue!);
            var client = _supabaseService.GetClient();

            var user = await _supabaseService.GetClient()
                    .From<Users>()
                    .Filter("UId", Operator.Equals, uid)
                    .Single();
            if (user == null || user.Status != "active" || user.Uid != uid || user.Uid != id)
                return View("NotFound");

            if (!ModelState.IsValid)
            {
                model.ProfilePicUrl = !string.IsNullOrEmpty(user.ProfilePic)
                ? _storageService.BuildFileUrl("profile_pic", user.ProfilePic)
                : null;

                var artworks = await client
               .From<ProfileArtwork>()
               .Where(a => a.Uid == uid)
               .Order("created_at", Ordering.Ascending)
               .Get();

                model.Artworks = artworks.Models.Select(a => new ProfileArtworkAddEditViewModel
                {
                    ProfArtid = a.ProfArtid,
                    ImageUrl = _storageService.BuildFileUrl("profile_artwork", a.Image),
                    CreatedAt = a.CreatedAt,
                    UserId = a.Uid
                }).ToList();

                return View(model);
            }

            string? newProfilePicName = null;
            bool removePic = Request.Form["RemoveCoverImage"] == "true";
            if (model.ProfilePicFile != null && model.ProfilePicFile.Length > 0)
            {
                if (!string.IsNullOrEmpty(user.ProfilePic))
                    _storageService.Delete("profile_pic", user.ProfilePic);

                newProfilePicName = await _storageService.SaveAsync(
                    model.ProfilePicFile,
                    "profile_pic",
                    new[] { "image/jpeg", "image/png", "image/webp" }
                );
            }
            else if (removePic && !string.IsNullOrEmpty(user.ProfilePic))
            {
                _storageService.Delete("profile_pic", user.ProfilePic);
                newProfilePicName = null;
            }

            string? newBannerImageName = null;
            bool removeBanner = Request.Form["RemoveBannerImage"] == "true";
            if (model.BannerImageFile != null && model.BannerImageFile.Length > 0)
            {
                if (!string.IsNullOrEmpty(user.BannerImage))
                    _storageService.Delete("banner_image", user.BannerImage);

                newBannerImageName = await _storageService.SaveAsync(
                    model.BannerImageFile,
                    "banner_image",
                    new[] { "image/jpeg", "image/png", "image/webp" }
                );
            }
            else if (removeBanner && !string.IsNullOrEmpty(user.BannerImage))
            {
                _storageService.Delete("banner_image", user.BannerImage);
                newBannerImageName = null;
            }

            user.Nickname = model.Nickname;
            user.Gender = model.Gender;
            user.AboutMe = string.IsNullOrWhiteSpace(model.AboutMe) ? null : model.AboutMe;
            user.DOB = model.DOB;
            user.ProfilePic = newProfilePicName ?? (removePic ? null : user.ProfilePic);
            user.BannerImage = newBannerImageName ?? (removeBanner ? null : user.BannerImage);

            await client.From<Users>().Update(user);

            if (model.ArtworksToDelete != null)
            {
                var ids = model.ArtworksToDelete.Split(',').Select(int.Parse);
                foreach (var artId in ids)
                {
                    var artwork = await _supabaseService.GetClient()
                        .From<ProfileArtwork>()
                        .Filter("ProfArtid", Operator.Equals, artId)
                        .Single();

                    if (artwork != null)
                    {
                        await _supabaseService.GetClient().From<ProfileArtwork>().Where(a => a.ProfArtid == artId).Delete();
                        _storageService.Delete("profile_artwork", artwork.Image);
                    }
                }
            }

            if (model.ArtworkFiles != null && model.ArtworkFiles.Any())
            {
                foreach (var file in model.ArtworkFiles)
                {
                    var savedFileName = await _storageService.SaveAsync(file, "profile_artwork", new[] { "image/jpeg", "image/png", "image/webp" });

                    if (savedFileName != null)
                    {
                        var newArt = new ProfileArtwork
                        {
                            Uid = uid,
                            Image = savedFileName,
                            CreatedAt = DateTime.UtcNow
                        };
                        await _supabaseService.GetClient().From<ProfileArtwork>().Insert(newArt);
                    }
                }
            }

            return Redirect($"/Profile/{uidValue}");
        }

        [Authorize]
        [Route("Profile/dashboard")]
        public IActionResult Dashboard()
        {
            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidValue))
                return RedirectToAction("Login", "Account");
            var uid = int.Parse(uidValue!);

            var redirectResult = RedirectIfNotArtist(uid);
            if (redirectResult != null)
                return redirectResult;

            return Redirect("/Profile/dashboard/follower");
        }

        [Authorize]
        [Route("Profile/dashboard/follower")]
        public async Task<IActionResult> FollowerDashboard()
        {
            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidValue))
                return RedirectToAction("Login", "Account");

            var uid = int.Parse(uidValue!);

            var redirectResult = RedirectIfNotArtist(uid);
            if (redirectResult != null)
                return redirectResult;

            SetCacheHeaders();
            var client = _supabaseService.GetClient();

            var followersResponse = await client
                .From<Follow>()
                .Filter("artist_id", Operator.Equals, uid)
                .Order("f_date", Ordering.Descending)
                .Get();

            var followers = followersResponse.Models.ToList();
            var followerIds = followers.Select(f => f.FollowerId).Distinct().ToList();

            var followUsersResponse = await client
                .From<Users>()
                .Filter("UId", Operator.In, followerIds.Any() ? followerIds : new List<int> { -1 })
                .Get();

            var followUsers = followUsersResponse.Models.ToList();

            var followerViewModels = followers.Select(f =>
            {
                var user = followUsers.FirstOrDefault(u => u.Uid == f.FollowerId);

                if (user == null)
                    return null;

                string? profilePicUrl = string.IsNullOrEmpty(user.ProfilePic)
                    ? null
                    : _storageService.BuildFileUrl("profile_pic", user.ProfilePic);

                var displayName =
                    _enumService.ToEnum<usersRole>(user.Role) == usersRole.artist
                        ? user.Nickname
                        : user.Name;

                return new FollowerViewModel
                {
                    FId = f.FId,
                    FollowerId = f.FollowerId,
                    ArtistId = f.ArtistId,
                    FDate = f.FDate,
                    DisplayName = displayName ?? "",
                    ProfilePicUrl = profilePicUrl
                };
            })
            .Where(x => x != null)
            .ToList()!;

            var planResponse = await client
                .From<SubscriptionPlan>()
                .Filter("UId", Operator.Equals, uid)
                .Get();

            var plans = planResponse.Models.ToList();
            var planIds = plans.Select(p => p.SPId).Distinct().ToList();

            var subscriberResponse = await client
                .From<Subscription>()
                .Filter("SPId", Operator.In, planIds.Any() ? planIds : new List<int> { -1 })
                .Filter("payment_status", Operator.Equals, _enumService.ToStringValue(paymentStatus.paid))
                // Only include subscriptions with active status
                .Filter("status", Operator.Equals, _enumService.ToStringValue(subscriptionStatus.active))
                .Order("start_date", Ordering.Descending)
                .Get();

            var subscribers = subscriberResponse.Models.ToList();
            var subscriberIds = subscribers.Select(s => s.UId).Distinct().ToList();

            var subscriberUsersResponse = await client
                .From<Users>()
                .Filter("UId", Operator.In, subscriberIds.Any() ? subscriberIds : new List<int> { -1 })
                .Get();

            var subscriberUsers = subscriberUsersResponse.Models.ToList();

            var subscriberViewModels = subscribers.Select(s =>
            {
                var user = subscriberUsers.FirstOrDefault(u => u.Uid == s.UId);
                if (user == null)
                    return null;

                string? profilePicUrl = string.IsNullOrEmpty(user.ProfilePic)
                    ? null
                    : _storageService.BuildFileUrl("profile_pic", user.ProfilePic);

                var displayName =
                    _enumService.ToEnum<usersRole>(user.Role) == usersRole.artist
                        ? user.Nickname
                        : user.Name;

                // Get plan values for display
                string? planTitle = null;
                decimal? planPrice = null;
                if (plans != null && plans.Count > 0)
                {
                    var plan = plans.FirstOrDefault(p => p.SPId == s.SPId);
                    if (plan != null)
                    {
                        planTitle = plan.Title;
                        planPrice = plan.Price;
                    }
                }

                return new SubscriberViewModel
                {
                    SId = s.SId,
                    UId = s.UId,
                    SPId = s.SPId,
                    StartDate = s.StartDate,
                    EndDate = s.EndDate,
                    Status = _enumService.ToEnum<subscriptionStatus>(s.Status),
                    PaymentStatus = _enumService.ToEnum<paymentStatus>(s.PaymentStatus),
                    TotalAmount = s.TotalAmount,
                    ServiceTax = s.ServiceTax,
                    ProfilePicUrl = profilePicUrl,
                    DisplayName = displayName ?? ""
                    ,
                    PlanTitle = planTitle,
                    PlanPrice = planPrice
                };
            })
            .Where(x => x != null)
            .ToList()!;

            var chartData = new Dictionary<string, object>();
            foreach (var period in new[] { "30", "90", "365", "all" })
            {
                var cutoffDate = period == "all" ? DateTime.MinValue : DateTime.UtcNow.AddDays(-int.Parse(period));
                
                Func<DateTime, string> getGroupKey;
                Func<DateTime, string> getGroupLabel;
                
                if (period == "30")
                {
                    getGroupKey = d => d.ToString("yyyy-MM-dd");
                    getGroupLabel = d => d.ToString("MMM dd");
                }
                else if (period == "90")
                {
                    getGroupKey = d =>
                    {
                        var culture = System.Globalization.CultureInfo.InvariantCulture;
                        var weekNum = culture.Calendar.GetWeekOfYear(d, System.Globalization.CalendarWeekRule.FirstDay, DayOfWeek.Monday);
                        return $"{d.Year}-W{weekNum}";
                    };
                    getGroupLabel = d =>
                    {
                        var culture = System.Globalization.CultureInfo.InvariantCulture;
                        var weekNum = culture.Calendar.GetWeekOfYear(d, System.Globalization.CalendarWeekRule.FirstDay, DayOfWeek.Monday);
                        return $"W{weekNum} ({d:MMM dd})";
                    };
                }
                else if (period == "365")
                {
                    getGroupKey = d => d.ToString("yyyy-MM");
                    getGroupLabel = d => d.ToString("MMM yyyy");
                }
                else
                {
                    getGroupKey = d => d.ToString("yyyy-MM");
                    getGroupLabel = d => d.ToString("MMM yyyy");
                }
                
                var groupedFollowers = new Dictionary<string, int>();
                var groupedSubscribers = new Dictionary<string, int>();
                var groupLabels = new Dictionary<string, string>();
                
                foreach (var follower in followers.Where(f => f.FDate >= cutoffDate))
                {
                    var key = getGroupKey(follower.FDate);
                    if (!groupedFollowers.ContainsKey(key))
                        groupedFollowers[key] = 0;
                    groupedFollowers[key]++;
                    groupLabels[key] = getGroupLabel(follower.FDate);
                }
                
                foreach (var subscriber in subscribers.Where(s => s.StartDate.HasValue && s.StartDate >= cutoffDate))
                {
                    var key = getGroupKey(subscriber.StartDate!.Value);
                    if (!groupedSubscribers.ContainsKey(key))
                        groupedSubscribers[key] = 0;
                    groupedSubscribers[key]++;
                    groupLabels[key] = getGroupLabel(subscriber.StartDate!.Value);
                }
                
                var sortedKeys = groupedFollowers.Keys.Concat(groupedSubscribers.Keys).Distinct().OrderBy(k => k).ToList();
                var dates = sortedKeys.Select(k => groupLabels.ContainsKey(k) ? groupLabels[k] : k).ToList();
                var followerCounts = sortedKeys.Select(k => groupedFollowers.ContainsKey(k) ? groupedFollowers[k] : 0).ToList();
                var subscriberCounts = sortedKeys.Select(k => groupedSubscribers.ContainsKey(k) ? groupedSubscribers[k] : 0).ToList();
                
                chartData[$"period{period}"] = new
                {
                    dates = dates,
                    followers = followerCounts,
                    subscribers = subscriberCounts
                };
            }

            var vm = new FollowerDashViewModel
            {
                Followers = followerViewModels!,
                Subscribers = subscriberViewModels!,
                ChartData = chartData
            };

            ViewData["InitialDashTab"] = "follower";
            return View("DashboardLayout", vm);
        }

        [Authorize]
        [Route("Profile/dashboard/commissions")]
        public async Task<IActionResult> CommissionDashboard()
        {
            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidValue))
                return RedirectToAction("Login", "Account");
            var uid = int.Parse(uidValue!);

            var redirectResult = RedirectIfNotArtist(uid);
            if (redirectResult != null)
                return redirectResult;

            var client = _supabaseService.GetClient();

            // Get all commission plans owned by this artist
            var plansResp = await client
                .From<CommissionPlan>()
                .Filter("UId", Operator.Equals, uid)
                .Get();

            var planList = plansResp.Models.ToList();
            var planIds = planList.Select(p => p.CId).ToList();

            var vm = new Models.Profile.CommissionsDashViewModel();

            if (!planIds.Any())
            {
                vm.CompletedRequests = new List<Models.Profile.RequestSummaryViewModel>();
                vm.ChartData = new Dictionary<string, object>
                {
                    ["period30"] = new { labels = new[] { "Completed", "Revenue", "Tax", "Earned" }, values = new decimal[] { 0, 0, 0, 0 } },
                    ["period90"] = new { labels = new[] { "Completed", "Revenue", "Tax", "Earned" }, values = new decimal[] { 0, 0, 0, 0 } },
                    ["period365"] = new { labels = new[] { "Completed", "Revenue", "Tax", "Earned" }, values = new decimal[] { 0, 0, 0, 0 } },
                    ["periodall"] = new { labels = new[] { "Completed", "Revenue", "Tax", "Earned" }, values = new decimal[] { 0, 0, 0, 0 } }
                };

                ViewData["InitialDashTab"] = "commissions";
                return View("DashboardLayout", vm);
            }

            // Get completed requests for these plans
            var reqResp = await client
                .From<Request>()
                .Filter("CId", Operator.In, planIds)
                .Filter("status", Operator.Equals, _enumService.ToStringValue(requestStatus.completed))
                .Order("payment_date", Ordering.Descending)
                .Get();

            var requests = reqResp.Models.ToList();

            // Gather requester ids and plan titles map
            var userIds = requests.Select(r => r.UId).Distinct().ToList();
            var users = new Dictionary<int, Users>();
            if (userIds.Any())
            {
                var usersResp = await client.From<Users>().Filter("UId", Operator.In, userIds).Get();
                foreach (var u in usersResp.Models)
                    users[u.Uid] = u;
            }

            var planTitleMap = planList.ToDictionary(p => p.CId, p => p.Title ?? "");

            var summaries = new List<Models.Profile.RequestSummaryViewModel>();
            foreach (var r in requests)
            {
                string clientName = "Unknown";
                if (users.ContainsKey(r.UId))
                {
                    var u = users[r.UId];
                    clientName = !string.IsNullOrEmpty(u.Nickname) ? u.Nickname : (u.Name ?? u.Email ?? "Unknown");
                }

                summaries.Add(new Models.Profile.RequestSummaryViewModel
                {
                    RId = r.RId,
                    CId = r.CId,
                    ClientName = clientName,
                    RequestDate = r.PaymentDate ?? r.RequestDate,
                    TotalAmount = r.TotalAmount,
                    ServiceTax = r.ServiceTax,
                    PlanTitle = planTitleMap.ContainsKey(r.CId) ? planTitleMap[r.CId] : ""
                });
            }

            // Compute aggregates for periods
            var chartData = new Dictionary<string, object>();
            var periods = new Dictionary<string, int> {
                { "30", 30 }, { "90", 90 }, { "365", 365 }, { "all", int.MaxValue }
            };

            foreach (var kv in periods)
            {
                var key = kv.Key;
                var days = kv.Value;

                DateTime cutoff = days == int.MaxValue ? DateTime.MinValue : DateTime.UtcNow.AddDays(-days);

                var filtered = summaries.Where(s => s.RequestDate >= cutoff).ToList();

                var completedCount = filtered.Count;
                var totalCollected = filtered.Sum(s => s.TotalAmount);
                var totalTax = filtered.Sum(s => s.ServiceTax);
                var totalEarned = totalCollected - totalTax;

                chartData[$"period{key}"] = new
                {
                    labels = new[] { "Completed Requests", "Revenue", "Service Tax", "Total Earned" },
                    values = new decimal[] { completedCount, totalCollected, totalTax, totalEarned }
                };
            }

            vm.CompletedRequests = summaries;
            vm.ChartData = chartData.ToDictionary(x => x.Key, x => (object)x.Value);

            ViewData["InitialDashTab"] = "commissions";
            return View("DashboardLayout", vm);
        }

        [Route("Artist/register")]
        [Authorize]
        public IActionResult BecomeArtist()
        {
            var user = User;
            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidValue))
                return RedirectToAction("Login", "Account");

            if (user!.IsInRole(_enumService.ToStringValue(usersRole.artist)))
                return RedirectToAction("Index", "Home");

            return View();
        }

        [Route("Artist/register")]
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> BecomeArtist(BecomeArtistVM model)
        {
            var user = User;
            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidValue))
                return RedirectToAction("Login", "Account");
            var uid = int.Parse(uidValue!);
            var client = _supabaseService.GetClient();

            if (user!.IsInRole(_enumService.ToStringValue(usersRole.artist)))
                return RedirectToAction("Index", "Home");

            if (!ModelState.IsValid)
            {
                // Redisplay the form with validation messages
                return View(model);
            }

            if (model.AgreeToTerms)
            {
                // Fetch current user record, set role and nickname, then update
                var userRecord = await client
                    .From<Users>()
                    .Where(u => u.Uid == uid)
                    .Single();

                if (userRecord != null)
                {
                    userRecord.Role = _enumService.ToStringValue(usersRole.artist);
                    userRecord.Nickname = model.Nickname;

                    await client.From<Users>().Update(userRecord);
                }

                // Refresh authentication cookie so the user's claims (role) reflect the DB update immediately
                try
                {
                    var refreshedUser = await client
                        .From<Users>()
                        .Where(u => u.Uid == uid)
                        .Single();

                    if (refreshedUser != null)
                    {
                        var claims = new List<Claim>
                        {
                            new Claim(ClaimTypes.NameIdentifier, refreshedUser.Uid.ToString()),
                            new Claim(ClaimTypes.Name, refreshedUser.Name ?? string.Empty),
                            new Claim(ClaimTypes.Email, refreshedUser.Email ?? string.Empty),
                            new Claim(ClaimTypes.Role, refreshedUser.Role ?? string.Empty),
                        };

                        var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

                        await HttpContext.SignInAsync(
                            CookieAuthenticationDefaults.AuthenticationScheme,
                            new ClaimsPrincipal(claimsIdentity),
                            new AuthenticationProperties
                            {
                                IsPersistent = true,
                                ExpiresUtc = DateTime.UtcNow.AddHours(3)
                            });
                    }
                }
                catch
                {
                    // If refreshing the cookie fails, swallow the exception to avoid blocking the user flow.
                    // The user can always re-login to refresh their claims.
                }

                return Redirect($"/Profile/{uidValue}");
            }
            else
            {
                return RedirectToAction("Index", "Home");
            }
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Follow(int id)
        {
            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(uidValue))
                return RedirectToAction("Login", "Account");
            var uid = int.Parse(uidValue!);

            if (uid == id)
                return RedirectToAction("Index", "Home");

            var client = _supabaseService.GetClient();

            var existingFollow = await client
                .From<Follow>()
                .Filter("follower_id", Operator.Equals, uid)
                .Filter("artist_id", Operator.Equals, id)
                .Single();

            if (existingFollow == null)
            {
                var follow = new Follow
                {
                    FollowerId = uid,
                    ArtistId = id,
                    FDate = DateTime.UtcNow
                };
                await client.From<Follow>().Insert(follow);
            }
            else
            {
                await client
                 .From<Follow>()
                 .Where(f => f.FId == existingFollow.FId)
                 .Delete();
            }

            return Redirect($"/Profile/{id}");
        }

        private async Task<ProfileViewModel?> BuildProfileViewModelAsync(int id)
        {
            var client = _supabaseService.GetClient();
            int uid = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)! ?? "0");

            var user = await client
                .From<Users>()
                .Where(u => u.Uid == id)
                .Single();

            if (user == null)
                return null;

            string? profileUrl = string.IsNullOrEmpty(user.ProfilePic)
                ? null
                : _storageService.BuildFileUrl("profile_pic", user.ProfilePic);

            string? bannerUrl = string.IsNullOrEmpty(user.BannerImage)
                ? null
                : _storageService.BuildFileUrl("banner_image", user.BannerImage);

            var artworkResponse = await client
                .From<ProfileArtwork>()
                .Where(a => a.Uid == id)
                .Order("created_at", Ordering.Ascending)
                .Get();

            var artworkTasks = artworkResponse.Models.Select(art =>
            {
                string? imageUrl = string.IsNullOrEmpty(art.Image)
                    ? null
                    : _storageService.BuildFileUrl("profile_artwork", art.Image);

                return Task.FromResult(new ProfileArtworkViewModel
                {
                    ProfArtid = art.ProfArtid,
                    ImageUrl = imageUrl
                });
            }).ToList();

            var artworks = (await Task.WhenAll(artworkTasks)).ToList();

            var currentUserEmail = User.FindFirstValue(ClaimTypes.Email);
            bool isOwner = currentUserEmail != null && currentUserEmail == user.Email;

            var postsResponse = await client
                .From<Posts>()
                .Filter("UId", Operator.Equals, id)
                .Filter("status", Operator.Equals, _enumService.ToStringValue(postsStatus.published))
                .Order("created_at", Ordering.Descending)
                .Range(0, 4)
                .Get();

            var latestPostsModels = postsResponse.Models.ToList();

            var spIds = latestPostsModels
            .Where(p => p.SPId.HasValue)
            .Select(p => (int)p.SPId!)
            .Distinct()
            .ToList();

            var planPrices = new Dictionary<int, decimal>();

            if (spIds.Any())
            {
                var pResponse = await client
                    .From<SubscriptionPlan>()
                    .Filter("SPId", Operator.In, spIds)
                    .Get();

                planPrices = pResponse.Models
                    .ToDictionary(p => p.SPId, p => p.Price);
            }

            var latestPosts = postsResponse.Models.Select(p =>
            {
                string? cover = null;
                if (!string.IsNullOrEmpty(p.CoverImage))
                    cover = _storageService.BuildFileUrl("post_cover", p.CoverImage);

                decimal? price = null;
                if (p.SPId.HasValue && planPrices.TryGetValue(p.SPId.Value, out var planPrice))
                    price = planPrice;

                return new PostViewModel
                {
                    PostId = p.PostId,
                    Title = p.Title ?? "",
                    CreatedAt = p.CreatedAt,
                    Content = p.Content,
                    Price = price,
                    CoverImageUrl = cover
                };
            }).ToList();

            var plansResponse = await client
            .From<SubscriptionPlan>()
            .Where(sp => sp.UId == id && sp.Status == "active")
            .Order("price", Ordering.Ascending)
            .Get();

            var tomorrow = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.ffffff+00");
            var subscriptionPlanTasks = plansResponse.Models.Select(async sp =>
            {
                string? img = null;
                if (!string.IsNullOrEmpty(sp.Image))
                    img = _storageService.BuildFileUrl("subscriptionPlan_cover", sp.Image);


                bool subscribed = false;
                var subscribeResponse = await client
                    .From<Subscription>()
                    .Filter("UId", Operator.Equals, uid)
                    .Filter("SPId", Operator.Equals, sp.SPId)
                    .Filter("end_date", Operator.GreaterThan, tomorrow)
                    .Filter("status", Operator.Equals, _enumService.ToStringValue(subscriptionStatus.active))
                    .Filter("payment_status", Operator.Equals, _enumService.ToStringValue(paymentStatus.paid))
                    .Single();

                if (subscribeResponse != null)
                    subscribed = true;

                return new SubscriptionPlanViewModel
                {
                    SPId = sp.SPId,
                    Title = sp.Title ?? "",
                    Price = sp.Price,
                    Description = sp.Description,
                    ImageUrl = img,
                    Subscribed = subscribed
                };
            }).ToList();

            var subscriptionPlans = (await Task.WhenAll(subscriptionPlanTasks)).ToList();

            bool isFollow = false;
            if (uid != 0 && uid != id)
            {
                var followResponse = await client
                    .From<Follow>()
                    .Filter("follower_id", Operator.Equals, uid)
                    .Filter("artist_id", Operator.Equals, id)
                    .Single();
                if (followResponse != null)
                    isFollow = true;
            }

            return new ProfileViewModel
            {
                UserId = user.Uid,
                Username = user.Username,
                Name = user.Name,
                Nickname = user.Nickname,
                Role = _enumService.ToEnum<usersRole>(user.Role),
                CreatedAt = user.CreatedAt,
                ProfilePic = user.ProfilePic,
                ProfilePicUrl = profileUrl,
                BannerImageUrl = bannerUrl,
                AboutMe = user.AboutMe,
                DOB = user.DOB,
                IsOwner = isOwner,
                Artworks = artworks,
                LatestPosts = latestPosts,
                SubscriptionPlans = subscriptionPlans,
                IsFollow = isFollow
            };
        }

        private void SetCacheHeaders()
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var isAuthenticated = User.Identity?.IsAuthenticated ?? false;

            if (isAuthenticated && uid != null)
            {
                // Logged in user - no cache for their own profile
                Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, max-age=0";
                Response.Headers["Pragma"] = "no-cache";
            }
            else
            {
                // Public profile - cache for 5 minutes
                Response.Headers["Cache-Control"] = "public, max-age=300";
            }

            Response.Headers["Vary"] = "X-Requested-With, Authorization";
        }

        private IActionResult? RedirectIfNotArtist(int id)
        {
            var user = User;
            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidValue))
                return RedirectToAction("Login", "Account");
            var uid = int.Parse(uidValue!);

            if (!user!.IsInRole(_enumService.ToStringValue(usersRole.artist)) && uid == id)
                return RedirectToAction("Index", "Home");

            return null;
        }

        public class ToggleFollowRequest
        {
            [System.Text.Json.Serialization.JsonPropertyName("artistId")]
            public int ArtistId { get; set; }
        }

        [Authorize]
        [HttpPost]
        [Route("Profile/ToggleFollow")]
        public async Task<IActionResult> ToggleFollow([FromBody] ToggleFollowRequest req)
        {
            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0";
            var uid = int.Parse(uidValue!);

            if (uid <= 0)
                return Json(new { success = false, message = "User not authenticated" });

            if (req == null || req.ArtistId <= 0)
                return Json(new { success = false, message = "Invalid artist id" });

            if (uid == req.ArtistId)
                return Json(new { success = false, message = "Cannot follow yourself" });

            var client = _supabaseService.GetClient();

            try
            {
                // Verify artist exists
                var artist = await client
                    .From<Users>()
                    .Where(u => u.Uid == req.ArtistId)
                    .Single();

                // If no artist found by typed lambda, try using string filter as fallback
                if (artist == null)
                {
                    artist = await client
                        .From<Users>()
                        .Filter("UId", Operator.Equals, req.ArtistId)
                        .Single();
                }

                if (artist == null)
                    return Json(new { success = false, message = "Artist not found" });

                // Check if already following
                var existingFollow = await client
                    .From<Follow>()
                    .Where(f => f.FollowerId == uid && f.ArtistId == req.ArtistId)
                    .Single();

                if (existingFollow != null)
                {
                    // Unfollow
                    await client
                        .From<Follow>()
                        .Where(f => f.FId == existingFollow.FId)
                        .Delete();

                    return Json(new { success = true, isFollowing = false, message = "Unfollowed" });
                }
                else
                {
                    // Follow
                    var newFollow = new Follow
                    {
                        FollowerId = uid,
                        ArtistId = req.ArtistId,
                        FDate = DateTime.UtcNow
                    };

                    await client.From<Follow>().Insert(newFollow);

                    return Json(new { success = true, isFollowing = true, message = "Followed" });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Error: {ex.Message}" });
            }
        }

        [HttpGet]
        [Route("Profile/IsFollowing")]
        public async Task<IActionResult> IsFollowing(int artistId)
        {
            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidValue))
                return Json(new { isFollowing = false });

            var uid = int.Parse(uidValue!);
            if (uid == artistId) return Json(new { isFollowing = false });

            var client = _supabaseService.GetClient();

            var existingFollow = await client
                .From<Follow>()
                .Where(f => f.FollowerId == uid && f.ArtistId == artistId)
                .Single();

            if (existingFollow == null)
            {
                existingFollow = await client
                    .From<Follow>()
                    .Filter("follower_id", Operator.Equals, uid)
                    .Filter("artist_id", Operator.Equals, artistId)
                    .Single();
            }

            return Json(new { isFollowing = existingFollow != null });
        }
    }
}
