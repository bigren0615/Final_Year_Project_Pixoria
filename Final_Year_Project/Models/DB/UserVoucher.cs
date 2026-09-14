using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Final_Year_Project.Models.DB
{
    [Table("userVoucher")]
    public class UserVoucher : BaseModel
    {
        [PrimaryKey("userVoucherId")]
        public int UserVoucherId { get; set; }
        [Column("UId")]
        public int UId { get; set; }
        [Column("voucherId")]
        public int VoucherId { get; set; }
        [Column("status")]
        public string Status { get; set; } = string.Empty;
        [Column("created_at")]
        public DateTime CreatedAt { get; set; }
    }
}
