using Final_Year_Project.Enums;
using Final_Year_Project.Models.DB;
using Final_Year_Project.Models.FraudDetection;
using Final_Year_Project.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using System.Text.Json;
using static Supabase.Postgrest.Constants;

namespace Final_Year_Project.Controllers
{
    [Authorize(Roles = "admin")]
    public class ArtworkFraudDetectionController : Controller
    {
        private readonly SupabaseService _supabaseService;
        private readonly LocalStorageService _storageService;
        private readonly IFraudDetectionService _fraudDetectionService;
        private readonly EnumService _enumService;
        private readonly IConfiguration _config;
        private readonly ILogger<ArtworkFraudDetectionController> _logger;

        public ArtworkFraudDetectionController(
            SupabaseService supabaseService,
            LocalStorageService storageService,
            IFraudDetectionService fraudDetectionService,
            EnumService enumService,
            IConfiguration config,
            ILogger<ArtworkFraudDetectionController> logger)
        {
            _supabaseService = supabaseService;
            _storageService = storageService;
            _fraudDetectionService = fraudDetectionService;
            _enumService = enumService;
            _config = config;
            _logger = logger;
        }

        /// <summary>
        /// 3.1, 3.2, 3.4, 3.9 - Main page for scanning and monitoring posts
        /// Displays list of posts to scan and flagged artwork history
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ScanAndMonitor(string? search = null, int page = 1, int pageSize = 10)
        {
            var client = _supabaseService.GetClient();

            // Get all published posts with cover images
            var postsResponse = await client.From<Posts>()
                .Filter("status", Operator.Equals, _enumService.ToStringValue(postsStatus.published))
                .Order("created_at", Ordering.Descending)
                .Get();

            var posts = postsResponse.Models.ToList();

            // Get artists
            var artistIds = posts.Select(p => p.UId).Distinct().ToList();
            var artistsResponse = artistIds.Any()
                ? await client.From<Users>().Filter("UId", Operator.In, artistIds).Get()
                : null;
            var artists = artistsResponse?.Models.ToList() ?? new List<Users>();

            // Get fraud detection records
            var fraudResponse = await client.From<FraudDetection>().Get();
            var fraudRecords = fraudResponse.Models.ToList();

            // Apply search filter
            var filteredPosts = posts.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.ToLower();
                var matchingArtistIds = artists
                    .Where(a => (a.Nickname?.ToLower().Contains(searchLower) ?? false) || 
                                (a.Username?.ToLower().Contains(searchLower) ?? false))
                    .Select(a => a.Uid)
                    .ToList();

                filteredPosts = filteredPosts.Where(p =>
                    (p.Title?.ToLower().Contains(searchLower) ?? false) ||
                    matchingArtistIds.Contains(p.UId));
            }

            var postList = filteredPosts.ToList();

            // Pagination
            int totalCount = postList.Count;
            int totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            var paginatedPosts = postList.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            // Get content images for paginated posts
            var paginatedPostIds = paginatedPosts.Select(p => p.PostId).ToList();
            var postFilesResponse = paginatedPostIds.Any()
                ? await client.From<PostFiles>()
                    .Filter("post_id", Operator.In, paginatedPostIds)
                    .Filter("type", Operator.Equals, "image")
                    .Get()
                : null;
            var postFiles = postFilesResponse?.Models.ToList() ?? new List<PostFiles>();

            // Build post view models
            var postViewModels = paginatedPosts.Select(p =>
            {
                var imageUrl = !string.IsNullOrEmpty(p.CoverImage)
                    ? _storageService.BuildFileUrl("post_cover", p.CoverImage)
                    : "/images/no_image.jpg";
                var artist = artists.FirstOrDefault(a => a.Uid == p.UId);
                var lastFraud = fraudRecords.Where(f => f.PostId == p.PostId).OrderByDescending(f => f.FlaggedAt).FirstOrDefault();
                
                // Get content images for this post
                var contentImages = postFiles
                    .Where(pf => pf.PostId == p.PostId)
                    .Select(pf => _storageService.BuildFileUrl("post", pf.FileName ?? ""))
                    .Where(url => !string.IsNullOrEmpty(url))
                    .ToList();

                return new PostScanItemViewModel
                {
                    PostId = p.PostId,
                    PostTitle = p.Title,
                    ImageUrl = imageUrl,
                    ContentImageUrls = contentImages,
                    ArtistName = artist?.Nickname ?? artist?.Username ?? "Unknown",
                    ArtistId = artist?.Uid ?? 0,
                    CreatedAt = p.CreatedAt,
                    HasBeenScanned = lastFraud != null,
                    LastScanStatus = lastFraud?.Status,
                    LastScanDate = lastFraud?.FlaggedAt
                };
            }).ToList();

            // Get flagged artworks
            var flaggedFrauds = fraudRecords
                .Where(f => f.FraudRisk != "low")
                .OrderByDescending(f => f.FlaggedAt)
                .Take(50)
                .ToList();

            var flaggedViewModels = new List<FlaggedArtworkViewModel>();
            foreach (var fraud in flaggedFrauds)
            {
                var post = posts.FirstOrDefault(p => p.PostId == fraud.PostId);
                var artist = artists.FirstOrDefault(a => a.Uid == fraud.ArtistId);

                Posts? matchedPost = null;
                if (fraud.MatchedArtworkId.HasValue)
                {
                    matchedPost = posts.FirstOrDefault(p => p.PostId == fraud.MatchedArtworkId.Value);
                }

                Users? reviewer = null;
                if (fraud.ReviewedBy.HasValue)
                {
                    var reviewerResponse = await client.From<Users>()
                        .Filter("UId", Operator.Equals, fraud.ReviewedBy.Value)
                        .Single();
                    reviewer = reviewerResponse;
                }

                flaggedViewModels.Add(new FlaggedArtworkViewModel
                {
                    FraudId = fraud.FraudId,
                    PostId = fraud.PostId,
                    PostTitle = post?.Title ?? "Unknown",
                    ImageUrl = post != null && !string.IsNullOrEmpty(post.CoverImage)
                        ? _storageService.BuildFileUrl("post_cover", post.CoverImage)
                        : "/images/no_image.jpg",
                    ArtistName = artist?.Nickname ?? artist?.Username ?? "Unknown",
                    ArtistId = artist?.Uid ?? 0,
                    FlaggedAt = fraud.FlaggedAt,
                    FraudRisk = fraud.FraudRisk,
                    Status = fraud.Status,
                    DetectionType = fraud.DetectionType,
                    AiProbability = fraud.AiProbability,
                    PlagiarismSimilarity = fraud.PlagiarismSimilarity,
                    MatchedArtworkId = fraud.MatchedArtworkId,
                    MatchedPostTitle = matchedPost?.Title,
                    MatchedPostImageUrl = matchedPost != null && !string.IsNullOrEmpty(matchedPost.CoverImage)
                        ? _storageService.BuildFileUrl("post_cover", matchedPost.CoverImage)
                        : null,
                    EvidenceDetails = fraud.EvidenceDetails,
                    ReviewNotes = fraud.ReviewNotes,
                    ReviewedAt = fraud.ReviewedAt,
                    ReviewedByName = reviewer?.Nickname ?? reviewer?.Username
                });
            }

            // Build statistics
            var viewModel = new ScanAndMonitorViewModel
            {
                Posts = postViewModels,
                FlaggedArtworks = flaggedViewModels,
                TotalPosts = totalCount,
                TotalFlagged = fraudRecords.Count(f => f.Status == _enumService.ToStringValue(fraudDetectionStatus.flagged)),
                TotalUnderReview = fraudRecords.Count(f => f.Status == _enumService.ToStringValue(fraudDetectionStatus.under_review)),
                TotalVerified = fraudRecords.Count(f => f.Status == _enumService.ToStringValue(fraudDetectionStatus.verified)),
                TotalRejected = fraudRecords.Count(f => f.Status == _enumService.ToStringValue(fraudDetectionStatus.rejected)),
                SearchQuery = search,
                CurrentPage = page,
                TotalPages = totalPages,
                PageSize = pageSize
            };

            return View(viewModel);
        }

        /// <summary>
        /// 3.1, 3.2, 3.5 - Scan a specific post for fraud
        /// Scans both cover image AND all content images within the post (from PostFiles table)
        /// Returns separate results for cover and content
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ScanArtwork(int postId)
        {
            if (postId == 0)
            {
                return Json(new { success = false, error = "Post ID is required" });
            }

            var client = _supabaseService.GetClient();

            // Get post
            var post = await client.From<Posts>()
                .Filter("post_id", Operator.Equals, postId)
                .Single();

            if (post == null)
            {
                return Json(new { success = false, error = "Post not found" });
            }

            // Get artist info
            var artist = await client.From<Users>().Filter("UId", Operator.Equals, post.UId).Single();

            var riskPriority = new Dictionary<string, int> { { "low", 1 }, { "medium", 2 }, { "high", 3 } };
            
            // ====== SCAN COVER IMAGE ======
            FraudDetectionResponse? coverResult = null;
            if (!string.IsNullOrEmpty(post.CoverImage))
            {
                var coverPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "post_cover", post.CoverImage);
                if (System.IO.File.Exists(coverPath))
                {
                    using var coverStream = System.IO.File.OpenRead(coverPath);
                    coverResult = await _fraudDetectionService.DetectFraudAsync(
                        coverStream,
                        post.CoverImage,
                        postId.ToString(),
                        useCache: false);
                }
            }

            // ====== SCAN CONTENT IMAGES ======
            var postFilesResponse = await client.From<PostFiles>()
                .Filter("post_id", Operator.Equals, postId)
                .Filter("type", Operator.Equals, _enumService.ToStringValue(postFilesType.image))
                .Get();

            var postFiles = postFilesResponse.Models.ToList();
            var contentResults = new List<object>();
            FraudDetectionResponse? highestRiskContentResult = null;

            foreach (var postFile in postFiles)
            {
                var imagePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "post", postFile.FileName);
                if (!System.IO.File.Exists(imagePath))
                {
                    _logger.LogWarning("Image file not found: {FilePath}", imagePath);
                    continue;
                }

                using var imageStream = System.IO.File.OpenRead(imagePath);
                var fileResult = await _fraudDetectionService.DetectFraudAsync(
                    imageStream,
                    postFile.FileName,
                    postId.ToString(),
                    useCache: false);

                if (!fileResult.Success)
                {
                    _logger.LogWarning("Fraud detection failed for file {FileName}: {Error}", postFile.FileName, fileResult.Error);
                    continue;
                }

                contentResults.Add(new
                {
                    fileName = postFile.FileName,
                    imageUrl = _storageService.BuildFileUrl("post", postFile.FileName),
                    fraudRisk = fileResult.FraudRisk,
                    aiProbability = fileResult.AiDetection?.AiProbability ?? 0,
                    plagiarismSimilarity = fileResult.PlagiarismDetection?.Matches?.FirstOrDefault()?.Similarity ?? 0
                });

                var currentRiskLevel = riskPriority.GetValueOrDefault(fileResult.FraudRisk, 0);
                var highestRiskLevel = highestRiskContentResult != null ? riskPriority.GetValueOrDefault(highestRiskContentResult.FraudRisk, 0) : 0;

                if (highestRiskContentResult == null || currentRiskLevel > highestRiskLevel)
                {
                    highestRiskContentResult = fileResult;
                }
            }

            // Determine overall highest risk (between cover and content)
            FraudDetectionResponse? overallHighestRisk = null;
            string overallSource = "";
            
            if (coverResult != null && highestRiskContentResult != null)
            {
                var coverRiskLevel = riskPriority.GetValueOrDefault(coverResult.FraudRisk, 0);
                var contentRiskLevel = riskPriority.GetValueOrDefault(highestRiskContentResult.FraudRisk, 0);
                if (coverRiskLevel >= contentRiskLevel)
                {
                    overallHighestRisk = coverResult;
                    overallSource = "cover";
                }
                else
                {
                    overallHighestRisk = highestRiskContentResult;
                    overallSource = "content";
                }
            }
            else if (coverResult != null)
            {
                overallHighestRisk = coverResult;
                overallSource = "cover";
            }
            else if (highestRiskContentResult != null)
            {
                overallHighestRisk = highestRiskContentResult;
                overallSource = "content";
            }

            if (overallHighestRisk == null)
            {
                return Json(new { success = false, error = "No images found to scan in this post" });
            }

            var fraudResult = overallHighestRisk;

            // Determine detection type
            var isAiGenerated = fraudResult.AiDetection?.IsAiGenerated ?? false;
            var isPlagiarized = fraudResult.PlagiarismDetection?.IsPlagiarized ?? false;
            var detectionType = (isAiGenerated && isPlagiarized) ? "both" :
                               isAiGenerated ? "ai_detection" :
                               isPlagiarized ? "plagiarism" : "none";

            // Save fraud detection record if risk is not low
            if (fraudResult.FraudRisk != "low")
            {
                var matchedArtworkId = fraudResult.PlagiarismDetection?.Matches?
                    .FirstOrDefault(m => m.IsMatch)?.ImageId;

                int? matchedId = null;
                if (int.TryParse(matchedArtworkId, out var parsed))
                {
                    matchedId = parsed;
                }

                var evidenceDetails = JsonSerializer.Serialize(new
                {
                    ai_detection = fraudResult.AiDetection,
                    plagiarism_detection = fraudResult.PlagiarismDetection,
                    fraud_reasons = fraudResult.FraudReasons,
                    overall_source = overallSource,
                    cover_scan = coverResult,
                    content_scans = contentResults
                });

                // Check if a fraud record already exists for this post
                var existingFraudResponse = await client.From<FraudDetection>()
                    .Filter("post_id", Operator.Equals, postId)
                    .Get();
                
                var existingFraud = existingFraudResponse.Models.FirstOrDefault();

                if (existingFraud != null)
                {
                    // Update existing record
                    existingFraud.FlaggedAt = DateTime.UtcNow;
                    existingFraud.DetectionType = detectionType;
                    existingFraud.AiProbability = fraudResult.AiDetection?.AiProbability ?? 0;
                    existingFraud.PlagiarismSimilarity = fraudResult.PlagiarismDetection?.Matches?.FirstOrDefault()?.Similarity ?? 0;
                    existingFraud.MatchedArtworkId = matchedId;
                    existingFraud.FraudRisk = fraudResult.FraudRisk;
                    existingFraud.EvidenceDetails = evidenceDetails;

                    await client.From<FraudDetection>().Update(existingFraud);
                    _logger.LogInformation("Updated existing fraud detection record for post {PostId}", postId);
                }
                else
                {
                    // Create new record
                    var fraudRecord = new FraudDetection
                    {
                        PostId = postId,
                        ArtistId = post.UId,
                        FlaggedAt = DateTime.UtcNow,
                        DetectionType = detectionType,
                        AiProbability = fraudResult.AiDetection?.AiProbability ?? 0,
                        PlagiarismSimilarity = fraudResult.PlagiarismDetection?.Matches?.FirstOrDefault()?.Similarity ?? 0,
                        MatchedArtworkId = matchedId,
                        FraudRisk = fraudResult.FraudRisk,
                        Status = _enumService.ToStringValue(fraudDetectionStatus.flagged),
                        ArtistNotified = false,
                        EvidenceDetails = evidenceDetails
                    };

                    await client.From<FraudDetection>().Insert(fraudRecord);
                    _logger.LogInformation("Created new fraud detection record for post {PostId}", postId);

                    // 3.3 - Notify user about flagged artwork (only for new records)
                    if (artist != null && !string.IsNullOrEmpty(artist.Email))
                    {
                        try
                        {
                            await SendFlaggedArtworkNotificationAsync(artist, post, fraudResult);
                            fraudRecord.ArtistNotified = true;
                            fraudRecord.NotifiedAt = DateTime.UtcNow;
                            await client.From<FraudDetection>().Update(fraudRecord);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to send notification to artist {ArtistId}", artist.Uid);
                        }
                    }
                }
            }

            // Build response with SEPARATE cover and content results
            var coverImageUrl = !string.IsNullOrEmpty(post.CoverImage)
                ? _storageService.BuildFileUrl("post_cover", post.CoverImage)
                : "/images/no_image.jpg";

            var coverScanResult = coverResult != null ? new
            {
                imageUrl = coverImageUrl,
                fraudRisk = coverResult.FraudRisk,
                aiProbability = coverResult.AiDetection?.AiProbability ?? 0,
                plagiarismSimilarity = coverResult.PlagiarismDetection?.Matches?.FirstOrDefault()?.Similarity ?? 0,
                detectionType = (coverResult.AiDetection?.IsAiGenerated ?? false) && (coverResult.PlagiarismDetection?.IsPlagiarized ?? false) ? "both" :
                                (coverResult.AiDetection?.IsAiGenerated ?? false) ? "ai_detection" :
                                (coverResult.PlagiarismDetection?.IsPlagiarized ?? false) ? "plagiarism" : "none"
            } : null;

            var result = new
            {
                success = true,
                postId,
                postTitle = post.Title,
                artistName = artist?.Nickname ?? artist?.Username ?? "Unknown",
                overallRisk = fraudResult.FraudRisk,
                overallSource,
                coverScan = coverScanResult,
                contentScans = contentResults
            };

            return Json(result);
        }

        /// <summary>
        /// 3.4 - View details of a specific flagged artwork
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ReviewArtwork(int id)
        {
            var client = _supabaseService.GetClient();

            var fraud = await client.From<FraudDetection>()
                .Filter("flag_id", Operator.Equals, id)
                .Single();

            if (fraud == null)
            {
                return NotFound();
            }

            var post = await client.From<Posts>()
                .Filter("post_id", Operator.Equals, fraud.PostId)
                .Single();

            var artist = await client.From<Users>()
                .Filter("UId", Operator.Equals, fraud.ArtistId)
                .Single();

            Users? reviewer = null;
            if (fraud.ReviewedBy.HasValue)
            {
                reviewer = await client.From<Users>()
                    .Filter("UId", Operator.Equals, fraud.ReviewedBy.Value)
                    .Single();
            }

            // Get content images from PostFiles table
            var contentImageUrls = new List<string>();
            if (post != null)
            {
                var postFilesResponse = await client.From<PostFiles>()
                    .Filter("post_id", Operator.Equals, fraud.PostId)
                    .Filter("type", Operator.Equals, _enumService.ToStringValue(postFilesType.image))
                    .Get();

                if (postFilesResponse.Models != null)
                {
                    contentImageUrls = postFilesResponse.Models
                        .Where(pf => !string.IsNullOrEmpty(pf.FileName))
                        .Select(pf => _storageService.BuildFileUrl("post", pf.FileName))
                        .ToList();
                }
            }

            // Get plagiarism matches
            var matches = new List<PlagiarismMatchViewModel>();
            if (fraud.MatchedArtworkId.HasValue)
            {
                var matchedPost = await client.From<Posts>()
                    .Filter("post_id", Operator.Equals, fraud.MatchedArtworkId.Value)
                    .Single();

                matches.Add(new PlagiarismMatchViewModel
                {
                    PostId = fraud.MatchedArtworkId.Value,
                    PostTitle = matchedPost?.Title,
                    ImageUrl = matchedPost != null && !string.IsNullOrEmpty(matchedPost.CoverImage)
                        ? _storageService.BuildFileUrl("post_cover", matchedPost.CoverImage)
                        : null,
                    Similarity = fraud.PlagiarismSimilarity,
                    IsMatch = true
                });
            }

            var viewModel = new ReviewArtworkViewModel
            {
                FraudId = fraud.FraudId,
                PostId = fraud.PostId,
                PostTitle = post?.Title ?? "Unknown",
                ImageUrl = post != null && !string.IsNullOrEmpty(post.CoverImage)
                    ? _storageService.BuildFileUrl("post_cover", post.CoverImage)
                    : "/images/no_image.jpg",
                Content = post?.Content,
                ContentImageUrls = contentImageUrls,
                ArtistName = artist?.Nickname ?? artist?.Username ?? "Unknown",
                ArtistId = artist?.Uid ?? 0,
                ArtistEmail = artist?.Email,
                ArtistProfilePic = !string.IsNullOrEmpty(artist?.ProfilePic)
                    ? _storageService.BuildFileUrl("profile_pic", artist.ProfilePic)
                    : null,
                FlaggedAt = fraud.FlaggedAt,
                FraudRisk = fraud.FraudRisk,
                Status = fraud.Status,
                DetectionType = fraud.DetectionType,
                AiProbability = fraud.AiProbability,
                PlagiarismSimilarity = fraud.PlagiarismSimilarity,
                EvidenceDetails = fraud.EvidenceDetails,
                Matches = matches,
                ReviewNotes = fraud.ReviewNotes,
                ReviewedAt = fraud.ReviewedAt,
                ReviewedByName = reviewer?.Nickname ?? reviewer?.Username,
                ArtistNotified = fraud.ArtistNotified,
                NotifiedAt = fraud.NotifiedAt
            };

            return View(viewModel);
        }

        /// <summary>
        /// 3.6, 3.7 - Update the status of a flagged artwork and notify artist
        /// </summary>
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> UpdateStatus([FromBody] UpdateStatusViewModel model)
        {
            if (model == null || model.FraudId == 0)
            {
                return Json(new { success = false, error = "Invalid request" });
            }

            var validStatuses = new[] { "flagged", "under_review", "verified", "rejected" };
            if (!validStatuses.Contains(model.Status))
            {
                return Json(new { success = false, error = "Invalid status" });
            }

            var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uidValue) || !int.TryParse(uidValue, out var reviewerId))
            {
                return Json(new { success = false, error = "User not authenticated" });
            }

            var client = _supabaseService.GetClient();

            var fraud = await client.From<FraudDetection>()
                .Filter("flag_id", Operator.Equals, model.FraudId)
                .Single();

            if (fraud == null)
            {
                return Json(new { success = false, error = "Record not found" });
            }

            var previousStatus = fraud.Status;
            fraud.Status = model.Status;
            fraud.ReviewNotes = model.ReviewNotes;
            fraud.ReviewedBy = reviewerId;
            fraud.ReviewedAt = DateTime.UtcNow;

            await client.From<FraudDetection>().Update(fraud);

            // 3.7 - Notify artist about status change
            if (model.NotifyArtist && previousStatus != model.Status)
            {
                var artist = await client.From<Users>()
                    .Filter("UId", Operator.Equals, fraud.ArtistId)
                    .Single();

                var post = await client.From<Posts>()
                    .Filter("post_id", Operator.Equals, fraud.PostId)
                    .Single();

                if (artist != null && !string.IsNullOrEmpty(artist.Email))
                {
                    try
                    {
                        await SendStatusChangeNotificationAsync(artist, post, previousStatus, model.Status, model.ReviewNotes);
                        fraud.ArtistNotified = true;
                        fraud.NotifiedAt = DateTime.UtcNow;
                        await client.From<FraudDetection>().Update(fraud);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send status change notification to artist {ArtistId}", artist.Uid);
                    }
                }
            }

            return Json(new { success = true, message = "Status updated successfully" });
        }

        /// <summary>
        /// 3.8 - Download fraud detection report as JSON
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> DownloadReport(DateTime? startDate = null, DateTime? endDate = null)
        {
            var start = startDate ?? DateTime.UtcNow.AddDays(-30);
            var end = endDate ?? DateTime.UtcNow;

            var client = _supabaseService.GetClient();

            var fraudResponse = await client.From<FraudDetection>()
                .Filter("flagged_at", Operator.GreaterThanOrEqual, start.ToString("yyyy-MM-dd"))
                .Filter("flagged_at", Operator.LessThanOrEqual, end.ToString("yyyy-MM-dd"))
                .Order("flagged_at", Ordering.Descending)
                .Get();

            var fraudRecords = fraudResponse.Models.ToList();

            var report = new
            {
                GeneratedAt = DateTime.UtcNow,
                StartDate = start,
                EndDate = end,
                Statistics = new
                {
                    TotalScanned = fraudRecords.Count,
                    TotalFlagged = fraudRecords.Count(f => f.Status == "flagged"),
                    TotalUnderReview = fraudRecords.Count(f => f.Status == "under_review"),
                    TotalVerified = fraudRecords.Count(f => f.Status == "verified"),
                    TotalRejected = fraudRecords.Count(f => f.Status == "rejected"),
                    AiGeneratedCount = fraudRecords.Count(f => f.DetectionType == "ai_detection" || f.DetectionType == "both"),
                    PlagiarismCount = fraudRecords.Count(f => f.DetectionType == "plagiarism" || f.DetectionType == "both")
                },
                Records = fraudRecords.Select(f => new
                {
                    f.FraudId,
                    f.PostId,
                    f.ArtistId,
                    f.FlaggedAt,
                    f.DetectionType,
                    f.AiProbability,
                    f.PlagiarismSimilarity,
                    f.FraudRisk,
                    f.Status,
                    f.ReviewedAt,
                    f.ReviewNotes
                })
            };

            var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);
            var fileName = $"fraud_report_{start:yyyyMMdd}_{end:yyyyMMdd}.json";

            return File(bytes, "application/json", fileName);
        }

        /// <summary>
        /// 3.9 - Display statistics about fraud artwork
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Statistics()
        {
            var client = _supabaseService.GetClient();

            var fraudResponse = await client.From<FraudDetection>()
                .Order("flagged_at", Ordering.Descending)
                .Get();

            var fraudRecords = fraudResponse.Models.ToList();

            // Get related data for recent flagged artworks
            var recentFlagged = fraudRecords
                .Where(f => f.FraudRisk != "low")
                .Take(10)
                .ToList();

            var postIds = recentFlagged.Select(f => f.PostId).Distinct().ToList();
            var artistIds = recentFlagged.Select(f => f.ArtistId).Distinct().ToList();

            var postsResponse = postIds.Any()
                ? await client.From<Posts>().Filter("post_id", Operator.In, postIds).Get()
                : null;
            var posts = postsResponse?.Models.ToList() ?? new List<Posts>();

            var artistsResponse = artistIds.Any()
                ? await client.From<Users>().Filter("UId", Operator.In, artistIds).Get()
                : null;
            var artists = artistsResponse?.Models.ToList() ?? new List<Users>();

            // Build daily stats for last 30 days
            var dailyStats = new List<DailyStatViewModel>();
            for (int i = 29; i >= 0; i--)
            {
                var date = DateTime.UtcNow.Date.AddDays(-i);
                var dayRecords = fraudRecords.Where(f => f.FlaggedAt.Date == date).ToList();
                dailyStats.Add(new DailyStatViewModel
                {
                    Date = date,
                    Scanned = dayRecords.Count,
                    Flagged = dayRecords.Count(f => f.FraudRisk != "low"),
                    Resolved = dayRecords.Count(f => f.Status == "verified" || f.Status == "rejected")
                });
            }

            // Build recent flagged view models
            var recentFlaggedViewModels = recentFlagged.Select(f =>
            {
                var post = posts.FirstOrDefault(p => p.PostId == f.PostId);
                var artist = artists.FirstOrDefault(a => a.Uid == f.ArtistId);

                return new FlaggedArtworkViewModel
                {
                    FraudId = f.FraudId,
                    PostId = f.PostId,
                    PostTitle = post?.Title ?? "Unknown",
                    ImageUrl = post != null && !string.IsNullOrEmpty(post.CoverImage)
                        ? _storageService.BuildFileUrl("post_cover", post.CoverImage)
                        : "/images/no_image.jpg",
                    ArtistName = artist?.Nickname ?? artist?.Username ?? "Unknown",
                    ArtistId = artist?.Uid ?? 0,
                    FlaggedAt = f.FlaggedAt,
                    FraudRisk = f.FraudRisk,
                    Status = f.Status,
                    DetectionType = f.DetectionType,
                    AiProbability = f.AiProbability,
                    PlagiarismSimilarity = f.PlagiarismSimilarity
                };
            }).ToList();

            var viewModel = new FraudStatisticsViewModel
            {
                TotalScanned = fraudRecords.Count,
                TotalFlagged = fraudRecords.Count(f => f.Status == "flagged"),
                TotalUnderReview = fraudRecords.Count(f => f.Status == "under_review"),
                TotalVerified = fraudRecords.Count(f => f.Status == "verified"),
                TotalRejected = fraudRecords.Count(f => f.Status == "rejected"),
                AiGeneratedCount = fraudRecords.Count(f => f.DetectionType == "ai_detection" || f.DetectionType == "both"),
                PlagiarismCount = fraudRecords.Count(f => f.DetectionType == "plagiarism" || f.DetectionType == "both"),
                DailyStats = dailyStats,
                RecentFlaggedArtworks = recentFlaggedViewModels
            };

            return View(viewModel);
        }

        /// <summary>
        /// 3.5 - Compare two post images for similarity
        /// Compares the first image content from each post
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CompareImages(int postId1, int postId2)
        {
            if (postId1 == 0 || postId2 == 0)
            {
                return Json(new { success = false, error = "Both post IDs are required" });
            }

            var client = _supabaseService.GetClient();

            // Get posts
            var post1 = await client.From<Posts>()
                .Filter("post_id", Operator.Equals, postId1)
                .Single();

            var post2 = await client.From<Posts>()
                .Filter("post_id", Operator.Equals, postId2)
                .Single();

            if (post1 == null || post2 == null)
            {
                return Json(new { success = false, error = "Posts not found" });
            }

            // Get the first image file from each post's content
            var postFiles1Response = await client.From<PostFiles>()
                .Filter("post_id", Operator.Equals, postId1)
                .Filter("type", Operator.Equals, _enumService.ToStringValue(postFilesType.image))
                .Limit(1)
                .Get();

            var postFiles2Response = await client.From<PostFiles>()
                .Filter("post_id", Operator.Equals, postId2)
                .Filter("type", Operator.Equals, _enumService.ToStringValue(postFilesType.image))
                .Limit(1)
                .Get();

            var postFile1 = postFiles1Response.Models.FirstOrDefault();
            var postFile2 = postFiles2Response.Models.FirstOrDefault();

            if (postFile1 == null || postFile2 == null)
            {
                return Json(new { success = false, error = "No image content found in one or both posts" });
            }

            var imagePath1 = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "post", postFile1.FileName);
            var imagePath2 = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "post", postFile2.FileName);

            if (!System.IO.File.Exists(imagePath1) || !System.IO.File.Exists(imagePath2))
            {
                return Json(new { success = false, error = "Image files not found on server" });
            }

            using var stream1 = System.IO.File.OpenRead(imagePath1);
            using var stream2 = System.IO.File.OpenRead(imagePath2);

            var result = await _fraudDetectionService.CompareImagesAsync(
                stream1, postFile1.FileName,
                stream2, postFile2.FileName);

            if (!result.Success)
            {
                return Json(new { success = false, error = result.Error ?? "Comparison failed" });
            }

            return Json(new
            {
                success = true,
                similarity = result.Similarity,
                isSimilar = result.IsSimilar,
                threshold = result.Threshold,
                similarityPercentage = (int)Math.Round(result.Similarity * 100)
            });
        }

        /// <summary>
        /// Get service health status
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ServiceStatus()
        {
            var status = await _fraudDetectionService.GetHealthStatusAsync();
            var isHealthy = await _fraudDetectionService.IsHealthyAsync();

            return Json(new ServiceStatusViewModel
            {
                IsHealthy = isHealthy,
                Status = status.Status,
                ClipLoaded = status.ClipLoaded,
                Device = status.Device,
                StoredEmbeddings = status.StoredEmbeddings
            });
        }

        /// <summary>
        /// Helper method to send flagged artwork notification to artist
        /// </summary>
        private async Task SendFlaggedArtworkNotificationAsync(Users artist, Posts post, FraudDetectionResponse fraudResult)
        {
            string user = _config["Smtp:User"] ?? "";
            string pass = _config["Smtp:Pass"] ?? "";
            string host = _config["Smtp:Host"] ?? "";
            int port = _config.GetValue<int>("Smtp:Port");

            var mail = new MailMessage
            {
                From = new MailAddress(user, "Pixoria"),
                Subject = "Your Post Has Been Flagged for Review",
                IsBodyHtml = true
            };
            mail.To.Add(new MailAddress(artist.Email, artist.Name));

            var reasons = string.Join("<br/>• ", fraudResult.FraudReasons ?? new List<string>());
            mail.Body = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                    <h2 style='color: #e91e63;'>Post Flagged for Review</h2>
                    <p>Dear {artist.Nickname ?? artist.Name},</p>
                    <p>Your post <strong>{post.Title}</strong> has been flagged by our fraud detection system for review.</p>
                    <div style='background: #fff3e0; padding: 15px; border-radius: 8px; margin: 20px 0;'>
                        <p><strong>Risk Level:</strong> {fraudResult.FraudRisk.ToUpper()}</p>
                        <p><strong>Reasons:</strong></p>
                        <p>• {reasons}</p>
                    </div>
                    <p>Our team will review your post and notify you of the outcome. If you believe this is an error, please contact our support team.</p>
                    <p>Best regards,<br/>The Pixoria Team</p>
                </div>";

            using var smtp = new SmtpClient
            {
                Host = host,
                Port = port,
                EnableSsl = true,
                Credentials = new NetworkCredential(user, pass)
            };

            await smtp.SendMailAsync(mail);
        }

        /// <summary>
        /// Helper method to send status change notification to artist
        /// </summary>
        private async Task SendStatusChangeNotificationAsync(Users artist, Posts? post, string previousStatus, string newStatus, string? reviewNotes)
        {
            string user = _config["Smtp:User"] ?? "";
            string pass = _config["Smtp:Pass"] ?? "";
            string host = _config["Smtp:Host"] ?? "";
            int port = _config.GetValue<int>("Smtp:Port");

            var mail = new MailMessage
            {
                From = new MailAddress(user, "Pixoria"),
                Subject = $"Your Post Review Status Updated to {newStatus.Replace("_", " ").ToUpper()}",
                IsBodyHtml = true
            };
            mail.To.Add(new MailAddress(artist.Email, artist.Name));

            var statusColor = newStatus switch
            {
                "verified" => "#4caf50",
                "rejected" => "#f44336",
                "under_review" => "#ff9800",
                _ => "#9e9e9e"
            };

            var statusMessage = newStatus switch
            {
                "verified" => "Your post has been verified as authentic and is now back to normal status.",
                "rejected" => "Unfortunately, your post has been rejected due to fraud detection concerns. The post has been removed from our platform.",
                "under_review" => "Your post is currently under review by our team.",
                _ => "The status of your post has been updated."
            };

            mail.Body = $@"
                <div style='font-family: Arial, sans-serif; max-width: 600px; margin: 0 auto;'>
                    <h2 style='color: {statusColor};'>Post Status Updated</h2>
                    <p>Dear {artist.Nickname ?? artist.Name},</p>
                    <p>The review status of your post <strong>{post?.Title ?? "Unknown"}</strong> has been updated.</p>
                    <div style='background: #f5f5f5; padding: 15px; border-radius: 8px; margin: 20px 0;'>
                        <p><strong>Previous Status:</strong> {previousStatus.Replace("_", " ").ToUpper()}</p>
                        <p><strong>New Status:</strong> <span style='color: {statusColor}; font-weight: bold;'>{newStatus.Replace("_", " ").ToUpper()}</span></p>
                        {(!string.IsNullOrEmpty(reviewNotes) ? $"<p><strong>Review Notes:</strong> {reviewNotes}</p>" : "")}
                    </div>
                    <p>{statusMessage}</p>
                    <p>If you have any questions, please contact our support team.</p>
                    <p>Best regards,<br/>The Pixoria Team</p>
                </div>";

            using var smtp = new SmtpClient
            {
                Host = host,
                Port = port,
                EnableSsl = true,
                Credentials = new NetworkCredential(user, pass)
            };

            await smtp.SendMailAsync(mail);
        }
    }
}
