namespace Final_Year_Project.Models.FraudDetection
{
    /// <summary>
    /// View model for displaying a flagged artwork/post item in the list
    /// </summary>
    public class FlaggedArtworkViewModel
    {
        public int FraudId { get; set; }
        public int PostId { get; set; }
        public string? PostTitle { get; set; }
        public string? ImageUrl { get; set; }
        public string? ArtistName { get; set; }
        public int ArtistId { get; set; }
        public DateTime FlaggedAt { get; set; }
        public string FraudRisk { get; set; } = "low";
        public string Status { get; set; } = "flagged";
        public string DetectionType { get; set; } = "ai_detection";
        public float AiProbability { get; set; }
        public float PlagiarismSimilarity { get; set; }
        public int? MatchedArtworkId { get; set; }
        public string? MatchedPostTitle { get; set; }
        public string? MatchedPostImageUrl { get; set; }
        public string? EvidenceDetails { get; set; }
        public string? ReviewNotes { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewedByName { get; set; }
        public int ConfidencePercentage => (int)Math.Round(Math.Max(AiProbability, PlagiarismSimilarity) * 100);
        public bool IsAiGenerated => DetectionType == "ai_detection" || DetectionType == "both";
        public bool IsPlagiarized => DetectionType == "plagiarism" || DetectionType == "both";
    }

    /// <summary>
    /// View model for fraud detection scan results
    /// </summary>
    public class ScanResultViewModel
    {
        public bool Success { get; set; }
        public int PostId { get; set; }
        public string? PostTitle { get; set; }
        public string? ImageUrl { get; set; }
        public string? ArtistName { get; set; }
        public string FraudRisk { get; set; } = "low";
        public string DetectionType { get; set; } = "ai_detection";
        public float AiProbability { get; set; }
        public float Confidence { get; set; }
        public float PlagiarismSimilarity { get; set; }
        public string? EvidenceDetails { get; set; }
        public List<PlagiarismMatchViewModel> Matches { get; set; } = new();
        public int ConfidencePercentage => (int)Math.Round(Confidence * 100);
        public string? Error { get; set; }
        public bool IsAiGenerated => DetectionType == "ai_detection" || DetectionType == "both";
        public bool IsPlagiarized => DetectionType == "plagiarism" || DetectionType == "both";
    }

    public class PlagiarismMatchViewModel
    {
        public int PostId { get; set; }
        public string? PostTitle { get; set; }
        public string? ImageUrl { get; set; }
        public float Similarity { get; set; }
        public bool IsMatch { get; set; }
        public int SimilarityPercentage => (int)Math.Round(Similarity * 100);
    }

    /// <summary>
    /// View model for the main scan and monitor page
    /// </summary>
    public class ScanAndMonitorViewModel
    {
        public List<PostScanItemViewModel> Posts { get; set; } = new();
        public List<FlaggedArtworkViewModel> FlaggedArtworks { get; set; } = new();
        public ScanResultViewModel? LastScanResult { get; set; }
        public int TotalPosts { get; set; }
        public int TotalFlagged { get; set; }
        public int TotalUnderReview { get; set; }
        public int TotalVerified { get; set; }
        public int TotalRejected { get; set; }
        public string? SearchQuery { get; set; }
        public int CurrentPage { get; set; } = 1;
        public int TotalPages { get; set; } = 1;
        public int PageSize { get; set; } = 10;
    }

    /// <summary>
    /// View model for post items in the scan list
    /// </summary>
    public class PostScanItemViewModel
    {
        public int PostId { get; set; }
        public string? PostTitle { get; set; }
        public string? ImageUrl { get; set; }
        public List<string> ContentImageUrls { get; set; } = new();
        public string? ArtistName { get; set; }
        public int ArtistId { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool HasBeenScanned { get; set; }
        public string? LastScanStatus { get; set; }
        public DateTime? LastScanDate { get; set; }
    }

    /// <summary>
    /// View model for reviewing a specific flagged artwork/post
    /// </summary>
    public class ReviewArtworkViewModel
    {
        public int FraudId { get; set; }
        public int PostId { get; set; }
        public string? PostTitle { get; set; }
        public string? ImageUrl { get; set; }
        public string? Content { get; set; }
        public List<string> ContentImageUrls { get; set; } = new();
        public string? ArtistName { get; set; }
        public int ArtistId { get; set; }
        public string? ArtistEmail { get; set; }
        public string? ArtistProfilePic { get; set; }
        public DateTime FlaggedAt { get; set; }
        public string FraudRisk { get; set; } = "low";
        public string Status { get; set; } = "flagged";
        public string DetectionType { get; set; } = "ai_detection";
        public float AiProbability { get; set; }
        public float PlagiarismSimilarity { get; set; }
        public string? EvidenceDetails { get; set; }
        public List<PlagiarismMatchViewModel> Matches { get; set; } = new();
        public string? ReviewNotes { get; set; }
        public DateTime? ReviewedAt { get; set; }
        public string? ReviewedByName { get; set; }
        public bool ArtistNotified { get; set; }
        public DateTime? NotifiedAt { get; set; }
        public int ConfidencePercentage => (int)Math.Round(Math.Max(AiProbability, PlagiarismSimilarity) * 100);
        public bool IsAiGenerated => DetectionType == "ai_detection" || DetectionType == "both";
        public bool IsPlagiarized => DetectionType == "plagiarism" || DetectionType == "both";
    }

    /// <summary>
    /// View model for updating artwork status
    /// </summary>
    public class UpdateStatusViewModel
    {
        public int FraudId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string? ReviewNotes { get; set; }
        public bool NotifyArtist { get; set; } = true;
    }

    /// <summary>
    /// View model for fraud detection statistics and reports
    /// </summary>
    public class FraudStatisticsViewModel
    {
        public int TotalScanned { get; set; }
        public int TotalFlagged { get; set; }
        public int TotalUnderReview { get; set; }
        public int TotalVerified { get; set; }
        public int TotalRejected { get; set; }
        
        public int AiGeneratedCount { get; set; }
        public int PlagiarismCount { get; set; }
        
        public float FlaggedPercentage => TotalScanned > 0 ? (float)TotalFlagged / TotalScanned * 100 : 0;
        public float ResolvedPercentage => TotalFlagged > 0 ? (float)(TotalVerified + TotalRejected) / TotalFlagged * 100 : 0;
        public float RejectionRate => TotalFlagged > 0 ? (float)TotalRejected / TotalFlagged * 100 : 0;
        
        public List<DailyStatViewModel> DailyStats { get; set; } = new();
        public List<FlaggedArtworkViewModel> RecentFlaggedArtworks { get; set; } = new();
        
        public DateTime? ReportStartDate { get; set; }
        public DateTime? ReportEndDate { get; set; }
    }

    public class DailyStatViewModel
    {
        public DateTime Date { get; set; }
        public int Scanned { get; set; }
        public int Flagged { get; set; }
        public int Resolved { get; set; }
    }

    /// <summary>
    /// View model for fraud detection report generation
    /// </summary>
    public class FraudReportViewModel
    {
        public DateTime GeneratedAt { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public FraudStatisticsViewModel Statistics { get; set; } = new();
        public List<FlaggedArtworkViewModel> FlaggedArtworks { get; set; } = new();
        public List<FlaggedArtworkViewModel> ResolvedCases { get; set; } = new();
        public List<FlaggedArtworkViewModel> RejectedCases { get; set; } = new();
    }

    /// <summary>
    /// View model for comparing two images
    /// </summary>
    public class CompareImagesViewModel
    {
        public string? Image1Url { get; set; }
        public string? Image2Url { get; set; }
        public string? Image1Name { get; set; }
        public string? Image2Name { get; set; }
        public float Similarity { get; set; }
        public bool IsSimilar { get; set; }
        public int SimilarityPercentage => (int)Math.Round(Similarity * 100);
    }

    /// <summary>
    /// View model for service health status
    /// </summary>
    public class ServiceStatusViewModel
    {
        public bool IsHealthy { get; set; }
        public string Status { get; set; } = "unknown";
        public bool ClipLoaded { get; set; }
        public string Device { get; set; } = "cpu";
        public int StoredEmbeddings { get; set; }
    }
}
