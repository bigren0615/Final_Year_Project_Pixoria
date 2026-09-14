using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Final_Year_Project.Models.DB
{
    [Table("fraud_detection")]
    public class FraudDetection : BaseModel
    {
        [PrimaryKey("flag_id", true)]
        public int FraudId { get; set; }

        [Column("post_id")]
        public int PostId { get; set; }

        [Column("artist_id")]
        public int ArtistId { get; set; }

        [Column("flagged_at")]
        public DateTime FlaggedAt { get; set; }

        [Column("detection_type")]
        public string DetectionType { get; set; } = "ai_detection"; // ai_detection, plagiarism, both

        [Column("ai_probability")]
        public float AiProbability { get; set; }

        [Column("plagiarism_similarity")]
        public float PlagiarismSimilarity { get; set; }

        [Column("matched_artwork_id")]
        public int? MatchedArtworkId { get; set; }

        [Column("fraud_risk")]
        public string FraudRisk { get; set; } = "low"; // low, medium, high

        [Column("status")]
        public string Status { get; set; } = "flagged"; // flagged, under_review, verified, rejected

        [Column("reviewed_by")]
        public int? ReviewedBy { get; set; }

        [Column("reviewed_at")]
        public DateTime? ReviewedAt { get; set; }

        [Column("review_notes")]
        public string? ReviewNotes { get; set; }

        [Column("artist_notified")]
        public bool ArtistNotified { get; set; }

        [Column("notified_at")]
        public DateTime? NotifiedAt { get; set; }

        [Column("evidence_details")]
        public string? EvidenceDetails { get; set; } // JSON object with evidence
    }
}
