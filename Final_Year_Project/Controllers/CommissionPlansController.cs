using Final_Year_Project.Enums;
using Final_Year_Project.Models.CommissionPlans;
using Final_Year_Project.Models.DB;
using Final_Year_Project.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Stripe;
using Supabase.Gotrue.Mfa;
using System.Numerics;
using System.Reactive.Joins;
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Security.Cryptography;
using static Supabase.Postgrest.Constants;

namespace Final_Year_Project.Controllers
{
    [Authorize(Policy = "NonAdminOnly")]
    public class CommissionPlansController : Controller
    {
        private readonly SupabaseService _supabaseService;
        private readonly LocalStorageService _storageService;
        private readonly EnumService _enumService;
        private readonly IPaymentService _paymentService;
        private readonly TagSuggestionService _tagSuggestionService;
        private readonly IImageTaggingService _imageTaggingService;

        public CommissionPlansController(SupabaseService supabaseService, LocalStorageService storageService, EnumService enumService, IPaymentService paymentService, TagSuggestionService tagSuggestionService, IImageTaggingService imageTaggingService)
        {
            _supabaseService = supabaseService;
            _storageService = storageService;
            _enumService = enumService;
            _paymentService = paymentService;
            _tagSuggestionService = tagSuggestionService;
            _imageTaggingService = imageTaggingService;
        }

        [AllowAnonymous]
        [Route("CommissionPlans")]
        public async Task<IActionResult> CommissionIndex(string? search = null, string? category = null, decimal? minPrice = null, decimal? maxPrice = null, int page = 1, int pageSize = 12)
        {
            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0";
            var uid = int.Parse(uidValue!);
            var client = _supabaseService.GetClient();

            // Fetch all available commission plans
            var commissionsResponse = await client
                .From<CommissionPlan>()
                .Filter("status", Operator.Equals, _enumService.ToStringValue(commissionPlanStatus.available))
                .Order("created_at", Ordering.Descending)
                .Get();

            var allCommissions = commissionsResponse.Models.ToList();

            // Fetch related artist data
            var artistIds = allCommissions.Select(c => c.UId).Distinct().ToList();
            var artistsResponse = artistIds.Any()
                ? await client.From<Users>().Filter("UId", Operator.In, artistIds).Get()
                : null;
            var artists = artistsResponse?.Models.ToList() ?? new List<Users>();

            var filtered = allCommissions.AsEnumerable();

            // Apply search filter (search by title OR artist nickname/username)
            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.Trim();
                var matchingArtistIds = artists
                    .Where(a => (!string.IsNullOrEmpty(a.Nickname) && a.Nickname.Contains(searchLower, StringComparison.OrdinalIgnoreCase))
                             || (!string.IsNullOrEmpty(a.Username) && a.Username.Contains(searchLower, StringComparison.OrdinalIgnoreCase)))
                    .Select(a => a.Uid)
                    .ToList();

                filtered = filtered.Where(c => (c.Title != null && c.Title.Contains(searchLower, StringComparison.OrdinalIgnoreCase))
                                            || matchingArtistIds.Contains(c.UId));
            }

            // Apply category filter
            if (!string.IsNullOrWhiteSpace(category))
            {
                filtered = filtered.Where(c => c.Category == category);
            }

            // Apply price range filter
            if (minPrice.HasValue)
            {
                filtered = filtered.Where(c => c.TargetPrice >= minPrice.Value);
            }
            if (maxPrice.HasValue)
            {
                filtered = filtered.Where(c => c.TargetPrice <= maxPrice.Value);
            }

            var filteredList = filtered.ToList();
            int totalCount = filteredList.Count;
            int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);

            if (page > totalPages && totalPages > 0)
                page = totalPages;

            // Get paginated results
            var commissionsData = filteredList
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            // Build view models with artist info
            var commissionViewModels = commissionsData.Select(c =>
            {
                var artist = artists.FirstOrDefault(a => a.Uid == c.UId);
                var artistProfilePicUrl = artist != null && !string.IsNullOrEmpty(artist.ProfilePic)
                    ? _storageService.BuildFileUrl("profile_pic", artist.ProfilePic)
                    : null;

                var imageUrl = !string.IsNullOrEmpty(c.Image)
                    ? _storageService.BuildFileUrl("commissionPlan_cover", c.Image)
                    : "/images/no_image.jpg";

                var daysAgo = (DateTime.UtcNow - c.CreatedAt).Days;

                return new CommissionPlanViewModel
                {
                    CId = c.CId,
                    UId = c.UId,
                    Title = c.Title ?? "",
                    Description = c.Description,
                    Category = _enumService.ToEnum<commissionPlanCategory>(c.Category),
                    TargetPrice = c.TargetPrice,
                    ImageUrl = imageUrl,
                    CreatedAt = c.CreatedAt,
                    Status = _enumService.ToEnum<commissionPlanStatus>(c.Status),
                    IsOwner = c.UId == uid,
                    ArtistName = artist?.Nickname ?? artist?.Name ?? "Unknown",
                    ArtistProfilePic = artistProfilePicUrl
                };
            }).ToList();

            ViewData["CurrentPage"] = page;
            ViewData["TotalPages"] = totalPages;
            ViewData["PageSize"] = pageSize;
            ViewData["SearchQuery"] = search;
            ViewData["CurrentCategory"] = category;
            ViewData["MinPrice"] = minPrice;
            ViewData["MaxPrice"] = maxPrice;
            ViewData["IsAuthenticated"] = uid != 0;
            ViewData["AllCategories"] = Enum.GetValues(typeof(commissionPlanCategory))
                .Cast<commissionPlanCategory>()
                .Select(e => _enumService.ToStringValue(e))
                .ToList();

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate";
                Response.Headers["Vary"] = "X-Requested-With";
                return PartialView("_CommissionsListPartial", commissionViewModels);
            }

            return View(commissionViewModels);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelRequest(int rId)
        {
            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidValue))
                return RedirectToAction("Login", "Account");

            var uid = int.Parse(uidValue!);

            var client = _supabaseService.GetClient();

            var request = await client
                .From<Request>()
                .Filter("RId", Operator.Equals, rId)
                .Single();

            if (request == null)
            {
                return NotFound();
            }
            var plan = await client.From<CommissionPlan>().Filter("CId", Operator.Equals, request.CId).Single();
            var isAllowedToCancel = request.UId == uid || (plan != null && plan.UId == uid);

            if (!isAllowedToCancel)
            {
                return RedirectToAction("ManageRequest");
            }
            if (_enumService.ToEnum<requestStatus>(request.Status) != requestStatus.requested || _enumService.ToEnum<paymentStatus>(request.PaymentStatus) != paymentStatus.paid)
            {
                return RedirectToAction("ManageRequest");
            }

            request.Status = _enumService.ToStringValue(requestStatus.cancelled);
            request.PaymentStatus = _enumService.ToStringValue(paymentStatus.refunded);
            await UpdateRequestWithEnumRetries(client, request);

            TempData["SuccessMessage"] = "Request cancelled and payment marked as refunded.";

            return RedirectToAction("ManageRequest");
        }
        [HttpPost]
        public async Task<IActionResult> SubmitRequest(CommissionPlanRequestVM model)
        {
            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidValue))
                return RedirectToAction("Login", "Account");

            if (!ModelState.IsValid)
                return View(model);

            var uid = int.Parse(uidValue);

            if (uid == model.Plan.UId)
                return RedirectToAction("Index", "Home");

            var client = _supabaseService.GetClient();

            var request = new Request
            {
                CId = model.Plan.CId,
                UId = uid,
                Description = model.Request.Description,
                Status = _enumService.ToStringValue(requestStatus.pending),
                RequestDate = DateTime.UtcNow,
                TotalAmount = model.Plan.TargetPrice,
                ServiceTax = model.Plan.TargetPrice * 0.12m,
                PaymentStatus = _enumService.ToStringValue(paymentStatus.unpaid),
                Deadline = null
            };

            var inserted = await client.From<Request>().Insert(request);
            var newRequest = inserted.Models.FirstOrDefault();

            if (newRequest == null)
                return RedirectToAction("Index", "Home");

            // Store selected tags in session so they can be inserted after successful payment
            try
            {
                var selectedTagString = Request.Form["SelectedTagIds"].FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(selectedTagString))
                {
                    HttpContext.Session.SetString($"SelectedTagIds_{newRequest.RId}", selectedTagString);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to store selected tags for request {newRequest.RId} in session: {ex.Message}");
            }

            var domain = $"{Request.Scheme}://{Request.Host}/";
            var session = _paymentService.CreateCheckoutSession(model.Plan.TargetPrice, domain, "CommissionPlans/Success", "CommissionPlans/Cancel");

            HttpContext.Session.SetString("RequestId", newRequest!.RId.ToString());
            return Redirect(session.Url);
        }

        public async Task<IActionResult> Success()
        {
            var rId = Convert.ToInt32(HttpContext.Session.GetString("RequestId"));

            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidValue))
                return RedirectToAction("Login", "Account");

            var client = _supabaseService.GetClient();
            var uid = int.Parse(uidValue!);

            var request = await client.From<Request>().Where(r => r.RId == rId && r.UId == uid).Single();
            if (request == null || (_enumService.ToEnum<requestStatus>(request.Status) != requestStatus.pending && _enumService.ToEnum<paymentStatus>(request.PaymentStatus) == paymentStatus.paid))
                return View("NotFound");

            request.Status = _enumService.ToStringValue(requestStatus.requested);
            request.PaymentStatus = _enumService.ToStringValue(paymentStatus.paid);
            request.PaymentDate = DateTime.UtcNow;

            await UpdateRequestWithEnumRetries(client, request);

            // After payment success, save any tags that were selected during the request
            try
            {
                var key = $"SelectedTagIds_{request.RId}";
                var selectedTagString = HttpContext.Session.GetString(key);
                if (!string.IsNullOrWhiteSpace(selectedTagString))
                {
                    var tagIds = selectedTagString.Split(',').Select(s => {
                        if (int.TryParse(s, out var id)) return id; return -1;
                    }).Where(i => i > 0).Distinct().Take(10).ToList();

                    foreach (var tId in tagIds)
                    {
                        var tag = await client.From<Tag>().Where(t => t.TagId == tId).Single();
                        if (tag == null) continue;

                        var tagReq = new TagRequest
                        {
                            CreatedAt = DateTime.UtcNow,
                            TagId = tId,
                            RId = request.RId
                        };

                        await client.From<TagRequest>().Insert(tagReq);
                    }

                    // clear session storage for this request
                    HttpContext.Session.Remove(key);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save tags for request {request.RId} after success: {ex.Message}");
            }

            var plan = await client.From<CommissionPlan>().Where(c => c.CId == request.CId).Single();

            return Redirect($"/CommissionPlans/request/{request.RId}");
        }

        [HttpGet]
        [Route("CommissionPlans/request/{id}")]
        public async Task<IActionResult> RequestDetail(int id)
        {
            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var uid = string.IsNullOrEmpty(uidValue) ? -1 : int.Parse(uidValue);

            var client = _supabaseService.GetClient();

            var request = await client.From<Request>().Filter("RId", Operator.Equals, id).Single();
            if (request == null)
                return NotFound();

            var plan = await client.From<CommissionPlan>().Filter("CId", Operator.Equals, request.CId).Single();

            var requester = await client.From<Users>().Filter("UId", Operator.Equals, request.UId).Single();
            var receiver = plan != null ? await client.From<Users>().Filter("UId", Operator.Equals, plan.UId).Single() : null;

            var tags = new List<string>();
            try
            {
                var trResp = await client.From<TagRequest>().Filter("RId", Operator.Equals, request.RId).Get();
                var trList = trResp.Models.ToList();
                foreach (var tr in trList)
                {
                    var tag = await client.From<Tag>().Filter("tag_id", Operator.Equals, tr.TagId).Single();
                    if (tag != null && !string.IsNullOrEmpty(tag.TagName))
                        tags.Add(tag.TagName);
                }
            }
            catch { }

            var artVMs = new List<RequestArtworkVM>();
            try
            {
                var artsResp = await client.From<RequestArtwork>().Filter("RId", Operator.Equals, request.RId).Order("created_at", Ordering.Descending).Get();
                var arts = artsResp.Models.ToList();
                foreach (var art in arts)
                {
                    artVMs.Add(new RequestArtworkVM
                    {
                        RAId = art.RAId,
                        CreatedAt = art.CreatedAt,
                        ImgName = art.ImgName,
                        Url = string.IsNullOrEmpty(art.ImgName) ? null : _storageService.BuildFileUrl("requestArtwork", art.ImgName),
                        Type = _enumService.ToEnum<requestArtworkType>(art.Type),
                        Comment = art.Comment
                    });
                }
            }
            catch { }

            var vm = new RequestDetailViewModel
            {
                RId = request.RId,
                CId = request.CId,
                PlanTitle = plan?.Title ?? "Unknown",
                RequesterId = request.UId,
                RequesterName = requester != null ? (_enumService.ToEnum<usersRole>(requester.Role) == usersRole.artist ? requester.Nickname : requester.Name) : "Unknown",
                RequesterProfilePic = requester != null && !string.IsNullOrEmpty(requester.ProfilePic) ? _storageService.BuildFileUrl("profile_pic", requester.ProfilePic) : null,
                ReceiverId = plan?.UId ?? -1,
                ReceiverName = receiver != null ? (_enumService.ToEnum<usersRole>(receiver.Role) == usersRole.artist ? receiver.Nickname : receiver.Name) : "Unknown",
                ReceiverProfilePic = receiver != null && !string.IsNullOrEmpty(receiver.ProfilePic) ? _storageService.BuildFileUrl("profile_pic", receiver.ProfilePic) : null,
                RequestDescription = request.Description,
                Status = _enumService.ToEnum<requestStatus>(request.Status),
                PaymentStatus = _enumService.ToEnum<paymentStatus>(request.PaymentStatus),
                RequestDate = request.RequestDate,
                Deadline = request.Deadline,
                TotalAmount = request.TotalAmount,
                ServiceTax = request.ServiceTax,
                Tags = tags,
                Artworks = artVMs,
                IsCurrentUserArtist = User.IsInRole("artist"),
                IsCurrentUserRequester = uid == request.UId,
                IsCurrentUserReceiver = uid == (plan?.UId ?? -1)
            };

            return View(vm);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadDraft(int rId, IFormFile file)
        {
            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidValue)) return RedirectToAction("Login", "Account");
            var uid = int.Parse(uidValue);

            var client = _supabaseService.GetClient();
            var request = await client.From<Request>().Filter("RId", Operator.Equals, rId).Single();
            if (request == null) return NotFound();

            var plan = await client.From<CommissionPlan>().Filter("CId", Operator.Equals, request.CId).Single();
            if (plan == null || plan.UId != uid) return RedirectToAction("RequestDetail", new { id = rId });

            if (_enumService.ToEnum<requestStatus>(request.Status) != requestStatus.approved)
                return RedirectToAction("RequestDetail", new { id = rId });

            if (file == null || file.Length == 0)
            {
                TempData["ErrorMessage"] = "No file uploaded.";
                return RedirectToAction("RequestDetail", new { id = rId });
            }

            var saved = await _storageService.SaveAsync(file, "requestArtwork", LocalStorageService.DefaultAllowedContentTypes);
            if (saved == null)
            {
                TempData["ErrorMessage"] = "Upload failed.";
                return RedirectToAction("RequestDetail", new { id = rId });
            }

            var newArt = new RequestArtwork
            {
                CreatedAt = DateTime.UtcNow,
                ImgName = saved,
                Type = _enumService.ToStringValue(requestArtworkType.drafted),
                RId = rId
            };

            try
            {
                await client.From<RequestArtwork>().Insert(newArt);
            }
            catch (Supabase.Postgrest.Exceptions.PostgrestException pex) when (pex.Message != null && pex.Message.Contains("invalid input value for enum", StringComparison.OrdinalIgnoreCase))
            {
                newArt.Type = newArt.Type.ToUpperInvariant();
                await client.From<RequestArtwork>().Insert(newArt);
            }

            request.Status = _enumService.ToStringValue(requestStatus.drafted);
            await UpdateRequestWithEnumRetries(client, request);

            TempData["SuccessMessage"] = "Draft uploaded.";
            return RedirectToAction("RequestDetail", new { id = rId });
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReviewDraft(int rId, int raId, bool approved, string? comment)
        {
            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidValue)) return RedirectToAction("Login", "Account");
            var uid = int.Parse(uidValue);

            var client = _supabaseService.GetClient();
            var request = await client.From<Request>().Filter("RId", Operator.Equals, rId).Single();
            if (request == null) return NotFound();

            if (request.UId != uid) return RedirectToAction("RequestDetail", new { id = rId });

            if (_enumService.ToEnum<requestStatus>(request.Status) != requestStatus.drafted)
                return RedirectToAction("RequestDetail", new { id = rId });

            var art = await client.From<RequestArtwork>().Filter("RAId", Operator.Equals, raId).Single();
            if (art == null) return RedirectToAction("RequestDetail", new { id = rId });

            if (approved)
            {
                art.Type = _enumService.ToStringValue(requestArtworkType.approved);
                try
                {
                    await client.From<RequestArtwork>().Update(art);
                }
                catch (Supabase.Postgrest.Exceptions.PostgrestException pex) when (pex.Message != null && pex.Message.Contains("invalid input value for enum", StringComparison.OrdinalIgnoreCase))
                {
                    art.Type = art.Type.ToUpperInvariant();
                    await client.From<RequestArtwork>().Update(art);
                }

                request.Status = _enumService.ToStringValue(requestStatus.draft_approved);
                await UpdateRequestWithEnumRetries(client, request);

                TempData["SuccessMessage"] = "Draft approved.";
            }
            else
            {
                if (string.IsNullOrWhiteSpace(comment))
                {
                    TempData["ErrorMessage"] = "Please provide a comment when rejecting the draft.";
                    return RedirectToAction("RequestDetail", new { id = rId });
                }

                art.Type = _enumService.ToStringValue(requestArtworkType.rejected);
                art.Comment = comment;
                try
                {
                    await client.From<RequestArtwork>().Update(art);
                }
                catch (Supabase.Postgrest.Exceptions.PostgrestException pex) when (pex.Message != null && pex.Message.Contains("invalid input value for enum", StringComparison.OrdinalIgnoreCase))
                {
                    art.Type = art.Type.ToUpperInvariant();
                    await client.From<RequestArtwork>().Update(art);
                }

                // When the sender rejects a draft, revert request back to approved so the artist can submit a new draft
                request.Status = _enumService.ToStringValue(requestStatus.approved);
                await UpdateRequestWithEnumRetries(client, request);

                TempData["SuccessMessage"] = "Draft rejected and sender's comment saved. Sender rejected the draft so request is open for a new draft from the artist.";
            }

            return RedirectToAction("RequestDetail", new { id = rId });
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UploadCompleted(int rId, IFormFile file)
        {
            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidValue)) return RedirectToAction("Login", "Account");
            var uid = int.Parse(uidValue);

            var client = _supabaseService.GetClient();
            var request = await client.From<Request>().Filter("RId", Operator.Equals, rId).Single();
            if (request == null) return NotFound();

            var plan = await client.From<CommissionPlan>().Filter("CId", Operator.Equals, request.CId).Single();
            if (plan == null || plan.UId != uid) return RedirectToAction("RequestDetail", new { id = rId });

            if (_enumService.ToEnum<requestStatus>(request.Status) != requestStatus.draft_approved)
                return RedirectToAction("RequestDetail", new { id = rId });

            if (file == null || file.Length == 0)
            {
                TempData["ErrorMessage"] = "No file uploaded.";
                return RedirectToAction("RequestDetail", new { id = rId });
            }

            var saved = await _storageService.SaveAsync(file, "requestArtwork", LocalStorageService.DefaultAllowedContentTypes);
            if (saved == null)
            {
                TempData["ErrorMessage"] = "Upload failed.";
                return RedirectToAction("RequestDetail", new { id = rId });
            }

            var newArt = new RequestArtwork
            {
                CreatedAt = DateTime.UtcNow,
                ImgName = saved,
                Type = _enumService.ToStringValue(requestArtworkType.completed),
                RId = rId
            };
            try
            {
                await client.From<RequestArtwork>().Insert(newArt);
            }
            catch (Supabase.Postgrest.Exceptions.PostgrestException pex) when (pex.Message != null && pex.Message.Contains("invalid input value for enum", StringComparison.OrdinalIgnoreCase))
            {
                newArt.Type = newArt.Type.ToUpperInvariant();
                await client.From<RequestArtwork>().Insert(newArt);
            }

            request.Status = _enumService.ToStringValue(requestStatus.completed);
            await UpdateRequestWithEnumRetries(client, request);

            TempData["SuccessMessage"] = "Final artwork uploaded and request marked as completed.";
            return RedirectToAction("RequestDetail", new { id = rId });
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReceiverReject(int rId)
        {
            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidValue)) return RedirectToAction("Login", "Account");
            var uid = int.Parse(uidValue);

            var client = _supabaseService.GetClient();
            var request = await client.From<Request>().Filter("RId", Operator.Equals, rId).Single();
            if (request == null) return NotFound();

            var plan = await client.From<CommissionPlan>().Filter("CId", Operator.Equals, request.CId).Single();
            if (plan == null || plan.UId != uid) return RedirectToAction("RequestDetail", new { id = rId });

            // Prevent rejecting a request that has already been completed
            if (_enumService.ToEnum<requestStatus>(request.Status) == requestStatus.completed)
            {
                TempData["ErrorMessage"] = "Cannot reject a request that has already been completed.";
                return RedirectToAction("RequestDetail", new { id = rId });
            }

            request.Status = _enumService.ToStringValue(requestStatus.rejected);
            if (_enumService.ToEnum<paymentStatus>(request.PaymentStatus) == paymentStatus.paid)
                request.PaymentStatus = _enumService.ToStringValue(paymentStatus.refunded);

            await UpdateRequestWithEnumRetries(client, request);

            TempData["SuccessMessage"] = "Request rejected.";
            return RedirectToAction("RequestDetail", new { id = rId });
        }

        public async Task<IActionResult> Cancel()
        {
            var rId = Convert.ToInt32(HttpContext.Session.GetString("RequestId"));

            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidValue))
                return RedirectToAction("Login", "Account");

            var client = _supabaseService.GetClient();
            var uid = int.Parse(uidValue!);

            var request = await client.From<Request>().Where(r => r.RId == rId && r.UId == uid).Single();
            if (request == null || (_enumService.ToEnum<requestStatus>(request.Status) != requestStatus.pending && _enumService.ToEnum<paymentStatus>(request.PaymentStatus) == paymentStatus.paid))
                return View("NotFound");

            request.Status = _enumService.ToStringValue(requestStatus.cancelled);
            request.PaymentStatus = _enumService.ToStringValue(paymentStatus.unpaid);

            await UpdateRequestWithEnumRetries(client, request);

            // Clear any selected tags stored in session for this request since it's cancelled
            try
            {
                HttpContext.Session.Remove($"SelectedTagIds_{request.RId}");
            }
            catch { /* ignore */ }

            var plan = await client.From<CommissionPlan>().Where(c => c.CId == request.CId).Single();

            return Redirect($"/Profile/{plan!.UId}/commissions");
        }

        [AllowAnonymous]
        [Route("CommissionPlans/{id:int}")]
        public async Task<IActionResult> Commission(int id)
        {
            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "0";
            var uid = int.Parse(uidValue!);
            bool isOwner = false;
            var client = _supabaseService.GetClient();

            var commissionPlan = await client
                .From<CommissionPlan>()
                .Where(u => u.CId == id)
                .Single();

            if (commissionPlan == null || commissionPlan.Status != _enumService.ToStringValue(commissionPlanStatus.available) && commissionPlan.Status != _enumService.ToStringValue(commissionPlanStatus.@private)) return View("NotFound");

            if (commissionPlan.UId == uid)
            {
                isOwner = true;
            }
            else
            {
                if (commissionPlan.Status == _enumService.ToStringValue(commissionPlanStatus.@private)) return View("NotFound");
            }

            string? imageUrl = null;
            if (!string.IsNullOrEmpty(commissionPlan.Image))
                imageUrl = _storageService.BuildFileUrl("commissionPlan_cover", commissionPlan.Image);

            // Fetch artist info
            string? artistName = null;
            string? artistProfilePic = null;
            var artist = await client
                .From<Users>()
                .Where(u => u.Uid == commissionPlan.UId)
                .Single();
            
            if (artist != null)
            {
                artistName = !string.IsNullOrEmpty(artist.Nickname) ? artist.Nickname : artist.Email;
                if (!string.IsNullOrEmpty(artist.ProfilePic))
                    artistProfilePic = _storageService.BuildFileUrl("profile_pic", artist.ProfilePic);
            }

            var commissionPlanVM = new CommissionPlanViewModel
            {
                CId = commissionPlan.CId,
                UId = commissionPlan.UId,
                Title = commissionPlan.Title,
                Description = commissionPlan.Description,
                Category = _enumService.ToEnum<commissionPlanCategory>(commissionPlan.Category),
                TargetPrice = commissionPlan.TargetPrice,
                ImageUrl = imageUrl,
                Status = _enumService.ToEnum<commissionPlanStatus>(commissionPlan.Status),
                IsOwner = isOwner,
                ArtistName = artistName,
                ArtistProfilePic = artistProfilePic,
            };

            var vm = new CommissionPlanRequestVM
            {
                Plan = commissionPlanVM,
                Request = new RequestAddEditVM()
            };


            return View(vm);
        }

        [AllowAnonymous]
        [HttpGet]
        [Route("Tags/List")]
        public async Task<IActionResult> TagList(string? q)
        {
            var client = _supabaseService.GetClient();

            // Always perform server-side filtering and limit results to keep memory and latency low
            var limit = 30;
            if (string.IsNullOrWhiteSpace(q))
            {
                var allResp = await client.From<Tag>()
                    .Filter("status", Supabase.Postgrest.Constants.Operator.Equals, "available")
                    .Order("tag_name", Ordering.Ascending)
                    .Limit(limit)
                    .Get();
                var list = allResp.Models.Select(t => new { t.TagId, TagName = t.TagName ?? "" }).ToList();
                return Json(list);
            }

            var qTrim = q.Trim();

            // Use case-insensitive ilike filtering on server to avoid loading the entire table.
            // Supabase/Postgrest supports ilike for case-insensitive substring matches.
            try
            {
                var resp = await client.From<Tag>()
                    .Filter("tag_name", Supabase.Postgrest.Constants.Operator.ILike, $"%{qTrim}%")
                    .Filter("status", Supabase.Postgrest.Constants.Operator.Equals, "available")
                    .Order("tag_name", Ordering.Ascending)
                    .Limit(limit)
                    .Get();

                var filtered = resp.Models.Select(t => new { t.TagId, TagName = t.TagName ?? "" }).ToList();
                return Json(filtered);
            }
            catch
            {
                // Fallback: if the Postgrest client does not support ilike for whatever reason, do safe substring filter
                var resp = await client.From<Tag>()
                    .Order("tag_name", Ordering.Ascending)
                    .Limit(100)
                    .Get();
                var tags = resp.Models.ToList()
                    .Where(t => !string.IsNullOrEmpty(t.TagName) && t.TagName.Contains(qTrim, StringComparison.OrdinalIgnoreCase) && t.Status == "available")
                    .Select(t => new { t.TagId, TagName = t.TagName ?? "" })
                    .Take(limit)
                    .ToList();

                return Json(tags);
            }
        }

        // A lightweight suggestion API which inspects a description string and returns existing tags that match the extracted keywords.
        // This endpoint only returns tags that already exist in the database (via Supabase) and is limited to keep performance predictable.
        public class TagSuggestRequest
        {
            public string? Description { get; set; }
            public int Max { get; set; } = 8;
            // Optional: Image data (base64) for WD14 + CLIP-L image tagging
            public string? ImageBase64 { get; set; }
        }

        public class TagCreateRequest
        {
            public string? Name { get; set; }
        }

        [AllowAnonymous]
        [HttpPost]
        [Route("Tags/Suggest")]
        public async Task<IActionResult> SuggestTags([FromBody] TagSuggestRequest req)
        {
            var client = _supabaseService.GetClient();

            if (req == null)
            {
                Console.WriteLine("[SuggestTags] ❌ REQUEST IS NULL!");
                return Json(new List<object>());
            }

            Console.WriteLine($"[SuggestTags] ========== NEW REQUEST ==========");
            Console.WriteLine($"[SuggestTags] Has Description: {!string.IsNullOrWhiteSpace(req.Description)}");
            Console.WriteLine($"[SuggestTags] Has ImageBase64: {!string.IsNullOrWhiteSpace(req.ImageBase64)} (length: {req.ImageBase64?.Length ?? 0})");
            Console.WriteLine($"[SuggestTags] Max: {req.Max}");

            var found = new List<(int id, string name)>();
            var max = Math.Clamp(req.Max, 1, 20);
            var candidates = new List<string>();

            // Step 1: Extract text-based candidates from description
            if (!string.IsNullOrWhiteSpace(req.Description))
            {
                var desc = req.Description!.Trim();
                if (desc.Length >= 3)
                {
                    var textCandidates = _tagSuggestionService.ExtractCandidates(desc, 30).ToList();
                    candidates.AddRange(textCandidates);
                    Console.WriteLine($"[SuggestTags] Text candidates: {textCandidates.Count} tags extracted");
                }
            }

            // Step 2: Extract image-based candidates if image data provided (store separately for priority)
            var imageTags = new List<string>();
            var imageAnalysisProducedTags = false; // whether the image service actually produced tags
            if (!string.IsNullOrWhiteSpace(req.ImageBase64))
            {
                try
                {
                    Console.WriteLine($"[SuggestTags] Image data received: {req.ImageBase64.Length} characters (base64)");
                    
                    // FIRST: Check if image tagging service is healthy before proceeding
                    var isServiceHealthy = await _imageTaggingService.IsHealthyAsync();
                    if (!isServiceHealthy)
                    {
                        Console.WriteLine($"[SuggestTags] ⚠️ Image tagging service is DOWN - skipping image analysis");
                        Console.WriteLine($"[SuggestTags] Proceeding with text-based suggestions only");
                    }
                    else
                    {
                        // Fetch ALL character tags from database to use as CLIP candidates
                        Console.WriteLine($"[SuggestTags] Fetching all tags from database for CLIP candidates...");
                        var allTagsResponse = await client.From<Tag>()
                            .Select("tag_name")
                            .Limit(1000)
                            .Get();
                        
                        var allTags = new List<string>();
                        if (allTagsResponse.Models != null)
                        {
                            allTags = allTagsResponse.Models
                                .Where(t => !string.IsNullOrWhiteSpace(t.TagName))
                                .Select(t => t.TagName!)
                                .ToList();
                        }
                        Console.WriteLine($"[SuggestTags] Loaded {allTags.Count} total tags from database for CLIP");
                        
                        // Decode base64 image
                        var imageBytes = Convert.FromBase64String(req.ImageBase64);
                        Console.WriteLine($"[SuggestTags] Decoded image size: {imageBytes.Length} bytes");
                        
                        using var imageStream = new MemoryStream(imageBytes);
                        
                        // Analyze image using WD14 + CLIP-L (with timeout to prevent hanging)
                        Console.WriteLine($"[SuggestTags] Calling ImageTaggingService.AnalyzeImageAsync (max 65 seconds)...");
                        
                        using (var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(65)))
                        {
                            var imageTagResponse = await _imageTaggingService.AnalyzeImageAsync(imageStream, "suggestion_image", allTags);
                            
                            var tagCount = imageTagResponse?.Tags?.Count ?? 0;
                            Console.WriteLine($"[SuggestTags] ========== IMAGE RESPONSE ==========");
                            Console.WriteLine($"[SuggestTags] Response is null: {imageTagResponse == null}");
                            Console.WriteLine($"[SuggestTags] Success: {imageTagResponse?.Success}");
                            Console.WriteLine($"[SuggestTags] Tags count: {tagCount}");
                            Console.WriteLine($"[SuggestTags] Tags is null: {imageTagResponse?.Tags == null}");
                            Console.WriteLine($"[SuggestTags] Error: {imageTagResponse?.Error}");
                            if (imageTagResponse?.Tags != null && imageTagResponse.Tags.Count > 0)
                            {
                                Console.WriteLine($"[SuggestTags] Tags content: {string.Join(", ", imageTagResponse.Tags.Take(20))}");
                            }
                            Console.WriteLine($"[SuggestTags] ========== END IMAGE RESPONSE ==========");
                            
                            if (imageTagResponse?.Success == true && (imageTagResponse.Tags?.Any() == true))
                            {
                                // Log all image tags received
                                Console.WriteLine($"[SuggestTags] ✓✓✓ Image service returned {imageTagResponse.Tags!.Count} tags: {string.Join(", ", imageTagResponse.Tags!.Take(20))}");
                                
                                // Store image tags separately - they will be processed FIRST with exact matching
                                imageTags = imageTagResponse.Tags!.Take(20).ToList();
                                imageAnalysisProducedTags = imageTags.Any();
                                Console.WriteLine($"[SuggestTags] ✓✓✓ Stored {imageTags.Count} image tags for PRIORITY processing");
                            }
                            else
                            {
                                Console.WriteLine($"[SuggestTags] ✗✗✗ No tags from image service - Success: {imageTagResponse?.Success}, Tags: {imageTagResponse?.Tags?.Count ?? 0}, Error: {imageTagResponse?.Error}");
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Image tagging timed out - fall back to text suggestions
                    Console.WriteLine($"[SuggestTags] Image tagging timeout - falling back to text suggestions");
                }
                catch (Exception ex)
                {
                    // Log but don't fail - fall back to text suggestions
                    Console.WriteLine($"[SuggestTags] Image tagging failed with exception: {ex.GetType().Name} - {ex.Message}");
                    if (ex.InnerException != null)
                        Console.WriteLine($"[SuggestTags] Inner exception: {ex.InnerException.Message}");
                }
            }
            else
            {
                Console.WriteLine($"[SuggestTags] No image data provided");
            }

            // Process image tags FIRST with exact matching to maintain their priority order
            if (imageTags.Any())
            {
                Console.WriteLine($"[SuggestTags] *** PROCESSING {imageTags.Count} IMAGE TAGS WITH EXACT MATCHING (PRIORITY) ***");
                foreach (var imgTag in imageTags)
                {
                    if (found.Count >= max) break;
                    try
                    {
                        Console.WriteLine($"[SuggestTags] Searching for image tag: '{imgTag}'");
                        
                        // Try exact match first (case-insensitive, with wildcard for better matching)
                        var exactMatch = await client.From<Tag>()
                            .Filter("tag_name", Supabase.Postgrest.Constants.Operator.ILike, $"%{imgTag}%")
                            .Filter("status", Supabase.Postgrest.Constants.Operator.Equals, "available")
                            .Limit(1)
                            .Get();
                        
                        Console.WriteLine($"[SuggestTags] Exact match result for '{imgTag}': {exactMatch.Models?.Count ?? 0} matches");
                        
                        if (exactMatch.Models?.Any() == true)
                        {
                            var tag = exactMatch.Models.First();
                            if (!found.Any(f => f.id == tag.TagId) && !string.IsNullOrEmpty(tag.TagName))
                            {
                                Console.WriteLine($"[SuggestTags] ✓✓✓ IMAGE TAG '{imgTag}' FOUND IN DB (ID: {tag.TagId}, Name: {tag.TagName})");
                                found.Add((tag.TagId, tag.TagName));
                            }
                        }
                        else
                        {
                            // Not found - validate before auto-creating
                            if (!IsValidTagName(imgTag))
                            {
                                Console.WriteLine($"[SuggestTags] ✗✗✗ IMAGE TAG '{imgTag}' REJECTED - Invalid format (UUID/filename/corrupted data)");
                                continue;
                            }
                            
                            // Auto-create it since image analysis detected it
                            Console.WriteLine($"[SuggestTags] ✗✗✗ IMAGE TAG '{imgTag}' NOT IN DB - AUTO-CREATING");
                            try
                            {
                                var newTag = new Tag { TagName = imgTag, Status = "available" };
                                var insertResp = await client.From<Tag>().Insert(newTag);
                                var inserted = insertResp.Models?.FirstOrDefault();
                                if (inserted != null && !string.IsNullOrEmpty(inserted.TagName))
                                {
                                    Console.WriteLine($"[SuggestTags] ✓✓✓ AUTO-CREATED IMAGE TAG: '{imgTag}' (ID: {inserted.TagId})");
                                    found.Add((inserted.TagId, inserted.TagName));
                                }
                                else
                                {
                                    Console.WriteLine($"[SuggestTags] Insert response had no models or empty TagName");
                                }
                            }
                            catch (Exception autoCreateEx)
                            {
                                Console.WriteLine($"[SuggestTags] Failed to auto-create '{imgTag}': {autoCreateEx.Message}");
                                Console.WriteLine($"[SuggestTags] Exception: {autoCreateEx}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[SuggestTags] Error processing image tag '{imgTag}': {ex.Message}");
                        Console.WriteLine($"[SuggestTags] Exception: {ex}");
                    }
                }
                Console.WriteLine($"[SuggestTags] After processing {imageTags.Count} image tags, found: {found.Count} results");
            }
            else
            {
                Console.WriteLine($"[SuggestTags] No image tags to process!");
            }

            // Remove image tags from text candidates to avoid duplication
            var imageTagsSet = new HashSet<string>(imageTags, StringComparer.OrdinalIgnoreCase);
            candidates = candidates.Where(c => !imageTagsSet.Contains(c)).ToList();
            Console.WriteLine($"[SuggestTags] Filtered out image tags from text candidates, {candidates.Count} text candidates remain");

            // Step 3: Remove duplicates and limit
            candidates = candidates.Distinct(StringComparer.OrdinalIgnoreCase).Take(40).ToList();
            Console.WriteLine($"[SuggestTags] After dedup and limit: {candidates.Count} candidates");
            
            if (!candidates.Any())
            {
                Console.WriteLine($"[SuggestTags] No candidates found, returning empty list");
                return Json(new List<object>());
            }

            // Step 4: Query database for matching tags, auto-creating missing ones
            Console.WriteLine($"\n[SuggestTags] ========== DATABASE QUERY START ==========");
            Console.WriteLine($"[SuggestTags] DETECTED TAGS (from model): {string.Join(", ", candidates.Take(20))}");
            Console.WriteLine($"[SuggestTags] Total candidates: {candidates.Count}");
            Console.WriteLine($"[SuggestTags] Searching database for matches...");
            
            var matchedTags = new List<string>();
            var unmatchedTags = new List<string>();
            
            foreach (var cand in candidates)
            {
                if (found.Count >= max) break;
                try
                {
                    var resp = await client.From<Tag>()
                        .Filter("tag_name", Supabase.Postgrest.Constants.Operator.ILike, $"%{cand}%")
                        .Filter("status", Supabase.Postgrest.Constants.Operator.Equals, "available")
                        .Limit(8)
                        .Get();

                    if (resp.Models.Any())
                    {
                        var dbMatches = string.Join(", ", resp.Models.Select(m => m.TagName).Take(3));
                        Console.WriteLine($"[SuggestTags] ✓ Candidate '{cand}' -> Found {resp.Models.Count} DB matches: {dbMatches}");
                        matchedTags.Add(cand);
                        
                        foreach (var t in resp.Models)
                        {
                            if (found.Any(f => f.id == t.TagId)) continue;
                            if (string.IsNullOrEmpty(t.TagName)) continue;
                            found.Add((t.TagId, t.TagName));
                            if (found.Count >= max) break;
                        }
                    }
                    else
                    {
                        // No match found - try to auto-create the tag from image suggestions
                        Console.WriteLine($"[SuggestTags] ✗ Candidate '{cand}' -> NO database matches (searched for '%{cand}%')");
                        unmatchedTags.Add(cand);
                        
                        // Only auto-create if this came from image analysis (not just because an image was provided)
                        // Prevent auto-creating tags when the image tagging service was down or failed to produce tags
                        if (imageAnalysisProducedTags)
                        {
                            // Ensure we don't accidentally create filename-like or UUID tags
                            if (!IsValidTagName(cand))
                            {
                                Console.WriteLine($"[SuggestTags] ✗ Candidate '{cand}' rejected for auto-creation (invalid tag name)");
                                continue;
                            }
                            try
                            {
                                Console.WriteLine($"[SuggestTags] Auto-creating tag from image analysis: '{cand}'");
                                var newTag = new Tag { TagName = cand, Status = "available" };
                                var insertResp = await client.From<Tag>().Insert(newTag);
                                var inserted = insertResp.Models?.FirstOrDefault();
                                if (inserted != null && !string.IsNullOrEmpty(inserted.TagName))
                                {
                                    Console.WriteLine($"[SuggestTags] ✓ Successfully auto-created tag: '{cand}' (ID: {inserted.TagId})");
                                    found.Add((inserted.TagId, inserted.TagName));
                                    if (found.Count >= max) break;
                                }
                            }
                            catch (Exception autoCreateEx)
                            {
                                Console.WriteLine($"[SuggestTags] Failed to auto-create tag '{cand}': {autoCreateEx.Message}");
                                // Continue even if auto-create fails
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[SuggestTags] ✗ Error searching for '{cand}': {ex.Message}");
                    unmatchedTags.Add(cand);
                }
            }

            // Return existing tags in the format the UI expects
            var result = found.Select(f => new { TagId = f.id, TagName = f.name }).ToList();
            Console.WriteLine($"\n[SuggestTags] ========== RESULTS ==========");
            Console.WriteLine($"[SuggestTags] MATCHED IN DB: {string.Join(", ", matchedTags)}");
            Console.WriteLine($"[SuggestTags] NOT FOUND IN DB: {string.Join(", ", unmatchedTags.Take(10))}");
            Console.WriteLine($"[SuggestTags] FINAL RESULT: Returning {result.Count} tags");
            Console.WriteLine($"[SuggestTags] TAGS RETURNED: {string.Join(", ", result.Select(r => r.TagName))}");
            Console.WriteLine($"[SuggestTags] ========== END ==========\n");
            
            return Json(result);
        }

        // Create a new tag (authorized users only). Returns the created/existing tag.
        [Authorize]
        [HttpPost]
        [Route("Tags/Create")]
        public async Task<IActionResult> CreateTag([FromBody] TagCreateRequest req)
        {
            if (req == null || string.IsNullOrWhiteSpace(req.Name))
                return BadRequest(new { error = "Name required" });

            var name = req.Name.Trim();
            if (name.Length < 2 || name.Length > 60)
                return BadRequest(new { error = "Name must be between 2 and 60 characters" });

            var client = _supabaseService.GetClient();

            try
            {
                // First try to find an existing tag with case-insensitive match
                var findResp = await client.From<Tag>()
                    .Filter("tag_name", Supabase.Postgrest.Constants.Operator.ILike, name)
                    .Limit(50)
                    .Get();

                var existing = findResp.Models?.FirstOrDefault(t => string.Equals(t.TagName?.Trim(), name, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    return Json(new { TagId = existing.TagId, TagName = existing.TagName });
                }

                // Insert a new tag
                var t = new Tag { TagName = name, Status = "available" };
                var insResp = await client.From<Tag>().Insert(t);
                var inserted = insResp.Models?.FirstOrDefault();
                if (inserted != null)
                    return Json(new { TagId = inserted.TagId, TagName = inserted.TagName });

                // If insert didn't return a model for any reason, try to find by exact name and return that
                var retryFind = await client.From<Tag>()
                    .Filter("tag_name", Supabase.Postgrest.Constants.Operator.ILike, name)
                    .Limit(50)
                    .Get();
                var found = retryFind.Models?.FirstOrDefault(t2 => string.Equals(t2.TagName?.Trim(), name, StringComparison.OrdinalIgnoreCase));
                if (found != null)
                    return Json(new { TagId = found.TagId, TagName = found.TagName });

                // fallback
                return StatusCode(500, new { error = "Failed to insert tag" });
            }
            catch (Exception ex)
            {
                // Handle potential duplicate race: attempt to find existing tag again
                try
                {
                    var findResp2 = await client.From<Tag>()
                        .Filter("tag_name", Supabase.Postgrest.Constants.Operator.ILike, name)
                        .Limit(50)
                        .Get();

                    var existing2 = findResp2.Models?.FirstOrDefault(t => string.Equals(t.TagName?.Trim(), name, StringComparison.OrdinalIgnoreCase));
                    if (existing2 != null)
                        return Json(new { TagId = existing2.TagId, TagName = existing2.TagName });
                }
                catch { }

                return StatusCode(500, new { error = "Error creating tag", detail = ex.Message });
            }
        }

        [Authorize]
        [Route("CommissionPlans/add")]
        public IActionResult CommissionAdd()
        {
            var redirect = RedirectIfNotArtist();
            if (redirect != null)
                return redirect;

            ViewBag.CategoryList = _enumService.GetEnumSelectListItems<commissionPlanCategory>();
            return View();
        }

        [Authorize]
        [HttpPost]
        [Route("CommissionPlans/add")]
        public async Task<IActionResult> CommissionAdd(CommissionAddEditVM model)
        {
            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidValue))
            {
                return RedirectToAction("Login", "Account");
            }

            var redirect = RedirectIfNotArtist();
            if (redirect != null)
                return redirect;

            if (!ModelState.IsValid)
            {
                ViewBag.CategoryList = _enumService.GetEnumSelectListItems<commissionPlanCategory>();
                return View(model);
            }
            var uid = int.Parse(uidValue!);

            var client = _supabaseService.GetClient();

            string? fileName = null;
            if (model.Image != null && model.Image.Length > 0)
                fileName = await _storageService.SaveAsync(model.Image, "commissionPlan_cover", new[] { "image/jpeg", "image/png" });

            var commissionPlan = new CommissionPlan
            {
                CreatedAt = DateTime.UtcNow,
                UId = uid,
                Title = model.Title,
                Description = model.Description,
                Category = _enumService.ToStringValue(model.Category),
                TargetPrice = model.TargetPrice,
                Image = fileName ?? "",
                Status = _enumService.ToStringValue(commissionPlanStatus.available)
            };

            await _supabaseService.GetClient()
            .From<CommissionPlan>()
            .Insert(commissionPlan);

            TempData["SuccessMessage"] = "Commission added";
            return Redirect($"/Profile/{uidValue}/commissions");
        }

        [Authorize]
        [Route("CommissionPlans/{id:int}/edit")]
        public async Task<IActionResult> CommissionEdit(int id)
        {
            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidValue))
                return RedirectToAction("Login", "Account");
            var uid = int.Parse(uidValue!);

            var redirect = RedirectIfNotArtist();
            if (redirect != null)
                return redirect;

            var commission = await _supabaseService.GetClient()
                    .From<CommissionPlan>()
                    .Filter("CId", Operator.Equals, id)
                    .Filter("status", Operator.NotEqual, _enumService.ToStringValue(commissionPlanStatus.deleted))
                    .Single();
            if (commission == null || commission.UId != uid)
                return View("NotFound");

            var vm = new CommissionAddEditVM
            {
                CId = commission.CId,
                Title = commission.Title ?? "",
                Description = commission.Description,
                Category = _enumService.ToEnum<commissionPlanCategory>(commission.Category),
                TargetPrice = commission.TargetPrice,
                Status = _enumService.ToEnum<commissionPlanStatus>(commission.Status),
            };

            string? imageUrl = null;
            if (!string.IsNullOrEmpty(commission.Image))
                imageUrl = _storageService.BuildFileUrl("commissionPlan_cover", commission.Image);

            vm.ImageUrl = imageUrl;

            ViewBag.CategoryList = _enumService.GetEnumSelectListItems<commissionPlanCategory>();

            return View(vm);
        }

        [Authorize]
        [HttpPost]
        [Route("CommissionPlans/{id:int}/edit")]
        public async Task<IActionResult> CommissionEdit(int id, CommissionAddEditVM model)
        {
            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidValue))
                return RedirectToAction("Login", "Account");

            var redirect = RedirectIfNotArtist();
            if (redirect != null)
                return redirect;

            if (!ModelState.IsValid)
                return View(model);

            var uid = int.Parse(uidValue!);
            var client = _supabaseService.GetClient();

            var existingPlans = await _supabaseService.GetClient()
                .From<CommissionPlan>()
                .Filter("CId", Operator.Equals, id)
                .Filter("UId", Operator.Equals, uid)
                .Filter("status", Operator.NotEqual, _enumService.ToStringValue(commissionPlanStatus.deleted))
                .Single();

            if (existingPlans == null)
                return Redirect($"/Profile/{uidValue}/commissions");

            var commission = existingPlans;


            if (model.Image != null && model.Image.Length > 0)
            {
                if (!string.IsNullOrEmpty(commission.Image))
                {
                    _storageService.Delete("commissionPlan_cover", commission.Image);
                }

                var newFileName = await _storageService.SaveAsync(model.Image, "commissionPlan_cover", new[] { "image/jpeg", "image/png" });
                commission.Image = newFileName ?? "";
            }
            else if (model.RemoveImage)
            {
                if (!string.IsNullOrEmpty(commission.Image))
                    _storageService.Delete("commissionPlan_cover", commission.Image);

                commission.Image = "";
            }
            var updateResponse = await client
            .From<CommissionPlan>()
            .Where(c => c.CId == id && c.UId == uid)
            .Set(c => new KeyValuePair<object, object?>(c.Title, model.Title))
            .Set(c => new KeyValuePair<object, object?>(c.Description!, model.Description ?? ""))
            .Set(c => new KeyValuePair<object, object?>(c.Category, model.Category.ToString()))
            .Set(c => new KeyValuePair<object, object?>(c.TargetPrice, model.TargetPrice))
            .Set(c => new KeyValuePair<object, object?>(c.Image!, commission.Image ?? ""))
            .Update();

            if (updateResponse.Models.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "Failed to update the plan. Please try again.");
                return View(model);
            }

            TempData["SuccessMessage"] = "Plan updated";
            return Redirect($"/CommissionPlans/{id}");
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CommissionDelete(int id)
        {
            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidValue))
                return RedirectToAction("Login", "Account");

            var redirect = RedirectIfNotArtist();
            if (redirect != null)
                return redirect;

            var uid = int.Parse(uidValue!);

            var existingCommission = await _supabaseService.GetClient()
                .From<CommissionPlan>()
                .Filter("CId", Operator.Equals, id)
                .Filter("UId", Operator.Equals, uid)
                .Filter("status", Operator.NotEqual, _enumService.ToStringValue(commissionPlanStatus.deleted))
                .Single();

            if (existingCommission == null)
                return Redirect($"/Profile/{uidValue}/commissions");

            var response = await _supabaseService.GetClient()
                .From<CommissionPlan>()
                .Where(p => p.CId == id && p.UId == uid)
                .Set(p => new KeyValuePair<object, object?>(p.Status, _enumService.ToStringValue(commissionPlanStatus.deleted)))
                .Update();

            if (response.Models.Count == 0)
                return Redirect($"/Profile/{uidValue}/plans");
            else
            {
                if (!string.IsNullOrEmpty(existingCommission.Image))
                    _storageService.Delete("commissionPlan_cover", existingCommission.Image);
            }

            TempData["SuccessMessage"] = "Commission deleted";
            return Redirect($"/Profile/{uidValue}/commissions");
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeclineRequest(int rId)
        {
            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidValue))
                return RedirectToAction("Login", "Account");

            var uid = int.Parse(uidValue!);

            var client = _supabaseService.GetClient();

            var request = await client.From<Request>().Filter("RId", Operator.Equals, rId).Single();
            if (request == null)
                return NotFound();

            // Only artist who owns the plan can decline
            var plan = await client.From<CommissionPlan>().Filter("CId", Operator.Equals, request.CId).Single();
            if (plan == null || plan.UId != uid)
                return RedirectToAction("ManageRequest");

            // Only allow decline when requested and paid
            if (_enumService.ToEnum<requestStatus>(request.Status) != requestStatus.requested || _enumService.ToEnum<paymentStatus>(request.PaymentStatus) != paymentStatus.paid)
                return RedirectToAction("ManageRequest");

            request.Status = _enumService.ToStringValue(requestStatus.rejected);
            request.PaymentStatus = _enumService.ToStringValue(paymentStatus.refunded);

            await UpdateRequestWithEnumRetries(client, request);

            TempData["SuccessMessage"] = "Request declined and payment marked as refunded.";

            return RedirectToAction("ManageRequest");
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AcceptRequest(int rId)
        {
            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidValue))
                return RedirectToAction("Login", "Account");

            var uid = int.Parse(uidValue!);

            var client = _supabaseService.GetClient();

            var request = await client.From<Request>().Filter("RId", Operator.Equals, rId).Single();
            if (request == null)
                return NotFound();

            var plan = await client.From<CommissionPlan>().Filter("CId", Operator.Equals, request.CId).Single();
            if (plan == null || plan.UId != uid)
                return RedirectToAction("ManageRequest");

            if (_enumService.ToEnum<requestStatus>(request.Status) != requestStatus.requested)
                return RedirectToAction("ManageRequest");

            request.Status = _enumService.ToStringValue(requestStatus.approved);
            request.Deadline = DateTime.UtcNow.AddDays(60);
            await UpdateRequestWithEnumRetries(client, request);

            TempData["SuccessMessage"] = "Request accepted.";
            return RedirectToAction("ManageRequest");
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CommissionVisibility(int id)
        {
            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidValue))
                return RedirectToAction("Login", "Account");

            var redirect = RedirectIfNotArtist();
            if (redirect != null)
                return redirect;

            var uid = int.Parse(uidValue!);

            var existingCommission = await _supabaseService.GetClient()
            .From<CommissionPlan>()
            .Filter("CId", Operator.Equals, id)
            .Filter("UId", Operator.Equals, uid)
            .Filter("status", Operator.NotEqual, _enumService.ToStringValue(commissionPlanStatus.deleted))
            .Single();

            if (existingCommission == null)
                return Redirect($"/Profile/{uidValue}/commissions");

            if (existingCommission.Status == "private")
            {
                var response = await _supabaseService.GetClient()
                .From<CommissionPlan>()
                .Where(p => p.CId == id && p.UId == uid)
                .Set(p => new KeyValuePair<object, object?>(p.Status, _enumService.ToStringValue(commissionPlanStatus.available)))
                .Update();

                if (response.Models.Count == 0)
                    return Redirect($"/Profile/{uidValue}/plans");

                return Redirect($"/CommissionPlans/{id}");
            }
            else if (existingCommission.Status == "available")
            {
                var response = await _supabaseService.GetClient()
                .From<CommissionPlan>()
                .Where(p => p.CId == id && p.UId == uid)
                .Set(p => new KeyValuePair<object, object?>(p.Status, _enumService.ToStringValue(commissionPlanStatus.@private)))
                .Update();

                if (response.Models.Count == 0)
                    return Redirect($"/Profile/{uidValue}/plans");

                return Redirect($"/CommissionPlans/{id}");
            }
            else
                return Redirect($"/Profile/{uidValue}/commissions");
        }

        [Authorize]
        [Route("CommissionPlans/request")]
        public async Task<IActionResult> ManageRequest()
        {
            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidValue))
                return RedirectToAction("Login", "Account");

            var uid = int.Parse(uidValue!);
            var client = _supabaseService.GetClient();
            var isArtist = User.IsInRole("artist");

            var statuses = new List<string>
            {
                _enumService.ToStringValue(paymentStatus.paid),
                _enumService.ToStringValue(paymentStatus.refunded)
            };

            var sentRequestsResponse = await client
                .From<Request>()
                .Filter("UId", Operator.Equals, uid)
                .Filter("payment_status", Operator.In, statuses)
                .Order("request_date", Ordering.Descending)
                .Get();

            var sentRequests = sentRequestsResponse.Models.ToList();

            List<Request> receivedRequests = new();
            if (isArtist)
            {
                var plansResponse = await client
                    .From<CommissionPlan>()
                    .Filter("UId", Operator.Equals, uid)
                    .Get();

                var plans = plansResponse.Models.ToList();
                var planIds = plans.Select(p => p.CId).Distinct().ToList();

                if (planIds.Any())
                {
                    var receivedResponse = await client
                        .From<Request>()
                        .Filter("CId", Operator.In, planIds)
                        .Filter("payment_status", Operator.In, statuses)
                        .Order("request_date", Ordering.Descending)
                        .Get();

                    receivedRequests = receivedResponse.Models.ToList();
                }
            }

            var sentRequestsVM = await BuildRequestViewModels(sentRequests, client, true);
            var receivedRequestsVM = await BuildRequestViewModels(receivedRequests, client, false);

            var vm = new ManageRequestViewModel
            {
                SentRequests = sentRequestsVM,
                ReceivedRequests = receivedRequestsVM,
                IsArtist = isArtist
            };

            return View(vm);
        }

        private async Task<List<ManageRequestItemVM>> BuildRequestViewModels(List<Request> requests, Supabase.Client client, bool isSent)
        {
            var result = new List<ManageRequestItemVM>();

            // Preload related data to avoid N+1 queries
            var requestIds = requests.Select(r => r.RId).Distinct().ToList();
            var planIds = requests.Select(r => r.CId).Distinct().ToList();

            var plansDict = new Dictionary<int, CommissionPlan>();
            if (planIds.Any())
            {
                try
                {
                    var plansResp = await client.From<CommissionPlan>().Filter("CId", Operator.In, planIds).Get();
                    foreach (var p in plansResp.Models)
                        plansDict[p.CId] = p;
                }
                catch { }
            }

            // We'll decide which user ids to fetch after we resolved plans for sent requests
            var userIdsToFetch = new List<int>();
            if (isSent)
            {
                // for sent requests we need the plan owner (artist)
                userIdsToFetch.AddRange(plansDict.Values.Select(p => p.UId));
            }
            else
            {
                // for received requests we need the requester (user who sent the request)
                userIdsToFetch.AddRange(requests.Select(r => r.UId));
            }

            userIdsToFetch = userIdsToFetch.Distinct().ToList();

            var usersDict = new Dictionary<int, Users>();
            if (userIdsToFetch.Any())
            {
                try
                {
                    var usersResp = await client.From<Users>().Filter("UId", Operator.In, userIdsToFetch).Get();
                    foreach (var u in usersResp.Models)
                        usersDict[u.Uid] = u;
                }
                catch { }
            }

            // Fetch tagrequests in one go then fetch tags
            var tagRequestsByRequest = new Dictionary<int, List<TagRequest>>();
            var allTagIds = new HashSet<int>();
            if (requestIds.Any())
            {
                try
                {
                    var trResp = await client.From<TagRequest>().Filter("RId", Operator.In, requestIds).Get();
                    foreach (var tr in trResp.Models)
                    {
                        if (!tagRequestsByRequest.ContainsKey(tr.RId))
                            tagRequestsByRequest[tr.RId] = new List<TagRequest>();

                        tagRequestsByRequest[tr.RId].Add(tr);
                        allTagIds.Add(tr.TagId);
                    }
                }
                catch { }
            }

            var tagsDict = new Dictionary<int, Tag>();
            if (allTagIds.Any())
            {
                try
                {
                    var tagsResp = await client.From<Tag>().Filter("tag_id", Operator.In, allTagIds.ToList()).Get();
                    foreach (var t in tagsResp.Models)
                        tagsDict[t.TagId] = t;
                }
                catch { }
            }

            foreach (var request in requests)
            {
                try
                {
                    plansDict.TryGetValue(request.CId, out var plan);
                    if (plan == null)
                        continue;

                    var userIdToFetch = isSent ? plan.UId : request.UId;
                    usersDict.TryGetValue(userIdToFetch, out var user);

                    string? profilePicUrl = null;
                    if (user != null && !string.IsNullOrEmpty(user.ProfilePic))
                        profilePicUrl = _storageService.BuildFileUrl("profile_pic", user.ProfilePic);

                    var displayName = user != null
                        ? (_enumService.ToEnum<usersRole>(user.Role) == usersRole.artist ? user.Nickname : user.Name)
                        : "Unknown User";

                    var tags = new List<string>();
                    if (tagRequestsByRequest.TryGetValue(request.RId, out var tagReqs))
                    {
                        foreach (var tr in tagReqs)
                        {
                            if (tagsDict.TryGetValue(tr.TagId, out var tag) && !string.IsNullOrEmpty(tag.TagName))
                                tags.Add(tag.TagName);
                        }
                    }

                    result.Add(new ManageRequestItemVM
                    {
                        RId = request.RId,
                        CId = request.CId,
                        PlanTitle = plan.Title ?? "Unknown Plan",
                        UserName = displayName ?? "Unknown",
                        UserProfilePic = profilePicUrl,
                        RequestDescription = request.Description,
                        Status = _enumService.ToEnum<requestStatus>(request.Status),
                        PaymentStatus = _enumService.ToEnum<paymentStatus>(request.PaymentStatus),
                        RequestDate = request.RequestDate,
                        Deadline = request.Deadline == DateTime.MinValue ? null : request.Deadline,
                        TotalAmount = request.TotalAmount,
                        ServiceTax = request.ServiceTax,
                        Tags = tags
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error building request VM: {ex.Message}");
                }
            }

            return result;
        }

        [AllowAnonymous]
        [Route("CommissionPlans/guideline")]
        public IActionResult Guideline()
        {
            return View();
        }

        private IActionResult? RedirectIfNotArtist()
        {
            var user = User;

            if (user != null && !user.IsInRole("artist"))
                return RedirectToAction("Index", "Home");

            return null;
        }

        // Helper: Update Request record but handle possible Postgres enum case mismatches by retrying with uppercase values
        private async Task UpdateRequestWithEnumRetries(Supabase.Client client, Request request)
        {
            try
            {
                await client.From<Request>().Update(request);
            }
            catch (Supabase.Postgrest.Exceptions.PostgrestException pex) when (pex.Message != null && pex.Message.Contains("invalid input value for enum", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(request.Status))
                    request.Status = request.Status.ToUpperInvariant();
                if (!string.IsNullOrEmpty(request.PaymentStatus))
                    request.PaymentStatus = request.PaymentStatus.ToUpperInvariant();

                await client.From<Request>().Update(request);
            }
        }

        /// <summary>
        /// Validate that a tag is a real tag, not a filename or UUID.
        /// Prevents storing corrupted data like "9fa13a3a-6973-4fad-9076-6f7e6c93fddc.png"
        /// or "872299c5-fb68-4a59-882e-903d36ae7c81 png"
        /// </summary>
        private static bool IsValidTagName(string tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return false;

            tag = tag.Trim();

            // Reject if too short (likely corrupted)
            if (tag.Length < 2)
                return false;

            // Reject if contains UUID pattern anywhere (with optional file extension or space + extension)
            // This catches:
            // - 872299c5-fb68-4a59-882e-903d36ae7c81
            // - 872299c5-fb68-4a59-882e-903d36ae7c81.png
            // - 872299c5-fb68-4a59-882e-903d36ae7c81 png
            // - 872299c5-fb68-4a59-882e-903d36ae7c81 .png
            var uuidPattern = new System.Text.RegularExpressions.Regex(
                @"[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}",
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            
            if (uuidPattern.IsMatch(tag))
                return false;

            // Reject if has too many dots (likely filename)
            if (tag.Split('.').Length > 3)
                return false;

            // Reject if ends with common image/file extensions
            var invalidExtensions = new[] { ".png", ".jpg", ".jpeg", ".gif", ".webp", ".bmp", ".pdf", ".exe", ".dll", ".zip", "png", "jpg", "jpeg", "gif", "webp", "bmp", "pdf", "exe", "dll", "zip" };
            var lowerTag = tag.ToLower();
            if (invalidExtensions.Any(ext => lowerTag.EndsWith(ext)))
                return false;

            // Reject if contains control characters or spaces followed by file extensions (corrupted data)
            if (tag.Contains("\n") || tag.Contains("\r") || tag.Contains("\t"))
                return false;

            // Reject if contains space followed by what looks like a file extension
            if (System.Text.RegularExpressions.Regex.IsMatch(tag, @"\s+\.\w{2,4}$"))
                return false;

            // Reject if contains ONLY hex digits and hyphens (looks like UUID without proper formatting)
            if (System.Text.RegularExpressions.Regex.IsMatch(tag, @"^[0-9a-fA-F\s\-\.]+$") && tag.Contains("-"))
                return false;

            return true;
        }
    }
}
