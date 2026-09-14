using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Final_Year_Project.Models.DB
{
    [Table("token")]
    public class Token : BaseModel
    {
        [PrimaryKey("tokenId")]
        public int TokenId { get; set; }

        [Column("tokenValue")]
        public string? TokenValue { get; set; }

        [Column("expire")]
        public DateTime Expire { get; set; }

        [Column("UId")]
        public int UId { get; set; }

        [Column("otp")]
        public string? OTP { get; set; }

        [Column("email")]
        public string? email { get; set; }

        [Column("old_email")]
        public string? old_email { get; set; }
    }
}

