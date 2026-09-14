using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Final_Year_Project.Models.DB
{
    [Table("pendingUser")]
    public class PendingUser : BaseModel
    {
        [PrimaryKey("id")]
        public int Id { get; set; }

        [Column("token")]
        public string? Token { get; set; }

        [Column("createdAt")]
        public DateTime CreatedAt { get; set; }

        [Column("username")]
        public string Username { get; set; } = string.Empty;

        [Column("nickname")]
        public string NickName { get; set; } = string.Empty;

        [Column("name")]
        public string Name { get; set; } = string.Empty;

        [Column("gender")]
        public string Gender { get; set; } = string.Empty;

        [Column("email")]
        public string Email { get; set; } = string.Empty;

        [Column("password")]
        public string Password { get; set; } = string.Empty;

        [Column("phoneNumber")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Column("role")]
        public string Role { get; set; } = string.Empty;
    }
}

