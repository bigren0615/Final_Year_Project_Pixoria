using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Final_Year_Project.Models.DB
{
    [Table("users")]
    public class Users : BaseModel
    {
        [PrimaryKey("UId")]
        public int Uid { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("role")]
        public string Role { get; set; } = string.Empty;

        [Column("DOB")]
        public DateOnly? DOB { get; set; }

        [Column("username")]
        public string Username { get; set; } = string.Empty;

        [Column("name")]
        public string Name { get; set; } = string.Empty;
        [Column("nickname")]
        public string Nickname { get; set; } = string.Empty;

        [Column("gender")]
        public string Gender { get; set; } = string.Empty;

        [Column("email")]
        public string Email { get; set; } = string.Empty;

        [Column("password")]
        public string Password { get; set; } = string.Empty;

        [Column("phone_number")]
        public string PhoneNumber { get; set; } = string.Empty;

        [Column("profile_pic")]
        public string? ProfilePic { get; set; }

        [Column("banner_image")]
        public string? BannerImage { get; set; }

        [Column("status")]
        public string Status { get; set; } = string.Empty;
        [Column("about_me")]
        public string? AboutMe { get; set; } = string.Empty;
        [Column("reward_point")]
        public int? RewardPoint { get; set; }
    }
}

