using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Final_Year_Project.Models.DB
{
    [Table("subscription")]
    public class Subscription : BaseModel
    {
        [PrimaryKey("SId")]
        public int SId { get; set; }
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
        [Column("start_date")]
        public DateTime? StartDate { get; set; }
        [Column("end_date")]
        public DateTime? EndDate { get; set; }
        [Column("UId")]
        public int UId { get; set; }
        [Column("SPId")]
        public int SPId { get; set; }
        [Column("payment_date")]
        public DateTime? PaymentDate { get; set; }
        [Column("service_tax")]
        public decimal ServiceTax { get; set; }
        [Column("total_amount")]
        public decimal TotalAmount { get; set; }
        [Column("status")]
        public string Status { get; set; } = "active";
        [Column("is_renewal")]
        public bool? IsRenewal { get; set; }
        [Column("payment_status")]
        public string PaymentStatus { get; set; } = "unpaid";
    }
}
