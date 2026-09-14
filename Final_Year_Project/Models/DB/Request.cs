using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Final_Year_Project.Models.DB
{
    [Table("request")]
    public class Request : BaseModel
    {
        [PrimaryKey("RId")]
        public int RId { get; set; }
        [Column("CId")]
        public int CId { get; set; }
        [Column("UId")]
        public int UId { get; set; }
        [Column("description")]
        public string Description { get; set; } = string.Empty;
        [Column("status")]
        public string Status { get; set; } = string.Empty;
        [Column("request_date")]
        public DateTime RequestDate { get; set; }
        [Column("payment_date")]
        public DateTime? PaymentDate { get; set; }
        [Column("total_amount")]
        public decimal TotalAmount { get; set; }
        [Column("service_tax")]
        public decimal ServiceTax { get; set; }
        [Column("payment_status")]
        public string PaymentStatus { get; set; } = string.Empty;
        [Column("deadline")]
        public DateTime? Deadline { get; set; }
    }
}
