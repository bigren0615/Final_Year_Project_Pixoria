using Final_Year_Project.Enums;
using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Final_Year_Project.Models.DB
{
    [Table("commissionPlan")]
    public class CommissionPlan : BaseModel
    {
        [PrimaryKey("CId")]
        public int CId { get; set; }
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
        [Column("title")]
        public string Title { get; set; } = string.Empty;
        [Column("UId")]
        public int UId { get; set; }
        [Column("description")]
        public string Description { get; set; } = string.Empty;
        [Column("category")]
        public string Category { get; set; } = string.Empty;
        [Column("target_price")]
        public decimal TargetPrice { get; set; }
        [Column("image")]
        public string? Image { get; set; }
        [Column("status")]
        public string Status { get; set; } = "available";
    }
}
