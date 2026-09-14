using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Final_Year_Project.Models.DB
{
    [Table("subscriptionPlan")]
    public class SubscriptionPlan : BaseModel
    {
        [PrimaryKey("SPId")]
        public int SPId { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("UId")]
        public int UId { get; set; }

        [Column("title")]
        public string Title { get; set; } = string.Empty;

        [Column("description")]
        public string? Description { get; set; } = string.Empty;

        [Column("price")]
        public decimal Price { get; set; }

        [Column("image")]
        public string? Image { get; set; }

        [Column("status")]
        public string Status { get; set; } = "active";
    }
}
