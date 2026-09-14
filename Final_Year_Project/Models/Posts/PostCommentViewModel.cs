namespace Final_Year_Project.Models.Posts
{
    public class PostCommentViewModel
    {
        public int CommentId { get; set; }
        public int PostId { get; set; }
        public int UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string UserNickname { get; set; } = string.Empty;
        public string? UserProfilePicUrl { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Comment { get; set; } = string.Empty;
        public bool CanDelete { get; set; }
        public int? ParentId { get; set; }
        public List<PostCommentViewModel> Replies { get; set; } = new List<PostCommentViewModel>();
    }

    public class PostCommentsListViewModel
    {
        public List<PostCommentViewModel> Comments { get; set; } = new List<PostCommentViewModel>();
        public int TotalCount { get; set; }
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int PageSize { get; set; }
        public bool CanComment { get; set; }
        public bool IsAuthenticated { get; set; }
        public bool RequiresSubscription { get; set; }
        public bool CanViewComments { get; set; }
    }
}
