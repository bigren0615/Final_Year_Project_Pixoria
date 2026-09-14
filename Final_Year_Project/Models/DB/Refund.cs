using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Final_Year_Project.Models.DB
{
    [Table("refund")]
    public class Refund : BaseModel
    {
        [PrimaryKey("refundId")]
        public long RefundId { get; set; }

        [Column("orderItemId")]
        public long OrderItemId { get; set; }

        [Column("OId")]
        public string OId { get; set; }

        [Column("Uid")]
        public int UId { get; set; }

        [Column("reason")]
        public string? Reason { get; set; }

        [Column("status")]
        public string RefundStatus { get; set; } = string.Empty;

        [Column("dateTimeRequested")]
        public DateTime RequestDate { get; set; }

        [Column("dueDate")]
        public DateTime DueDate { get; set; }

        [Column("processedDate")]
        public DateTime? ProcessedDate { get; set; }

        [Column("createdAt")]
        public DateTime CreatedAt { get; set; }
    }

    [Table("refundImage")]
    public class RefundImage : BaseModel
    {
        [PrimaryKey("refundImageId")]
        public long RefundImageId { get; set; }

        [Column("refundId")]
        public long RefundId { get; set; }

        [Column("imagePath")]
        public string? ImagePath { get; set; }

        [Column("uploadedAt")]
        public DateTime UploadedAt { get; set; }
    }
}
