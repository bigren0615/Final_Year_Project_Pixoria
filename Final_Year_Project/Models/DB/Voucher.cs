using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Final_Year_Project.Models.DB
{
    [Table("voucher")]
    public class Voucher : BaseModel
    {
        [PrimaryKey("voucherId")]
        public int VoucherId { get; set; }
        [Column("voucherCode")]
        public string VoucherCode { get; set; } = string.Empty;
        [Column("voucherName")]
        public string VoucherName { get; set; } = string.Empty;
        [Column("description")]
        public string? Description { get; set; } = string.Empty;
        [Column("quantity")]
        public int Quantity { get; set; }
        [Column("value")]
        public decimal Value { get; set; }
        [Column("minSpend")]
        public decimal? MinSpend { get; set; }
    }
}
