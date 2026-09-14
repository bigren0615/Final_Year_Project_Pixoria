using Final_Year_Project.Enums;
using Final_Year_Project.Models.DB;
using System.ComponentModel.DataAnnotations;

namespace Final_Year_Project.Models.Posts
{
    public class PostsAddEditVM
    {
        public int? PostId { get; set; }
        [MaxLength(50)]
        public string Title { get; set; } = string.Empty;
        public string? Content { get; set; } = string.Empty;
        public IFormFile? CoverImage { get; set; }
        public string? CoverImageUrl { get; set; }
        public postsStatus Status { get; set; }
        public bool RemoveCoverImage { get; set; } = false;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public postPublishTarget PublishTarget { get; set; }
        public int? SelectedPlanId { get; set; }
        public List<SubscriptionPlan> Plans { get; set; } = new();
        // Comma-separated tag ids selected by user (hidden input)
        public string? SelectedTagIds { get; set; } = string.Empty;

        // Names of selected tags shown in UI
        public List<string>? SelectedTags { get; set; } = new();
    }
}
