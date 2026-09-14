using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

namespace Final_Year_Project.Models.DB
{
    [Table("chat_message")]
    public class ChatbotMessage : BaseModel
    {

        [PrimaryKey("id")]
        public int Id { get; set; }

        [Column("sender")]
        public string? Sender { get; set; }

        [Column("content")]
        public string? Content { get; set; }

        [Column("created_at")]
        public DateTime CreatedAt { get; set; }

        [Column("chatSessionId")]
        public int ChatSessionId { get; set; }

    }
}
