using Final_Year_Project.Enums;

namespace Final_Year_Project.Models.CommissionPlans
{
    public class RequestDetailViewModel
    {
        public int RId { get; set; }
        public int CId { get; set; }
        public string PlanTitle { get; set; } = string.Empty;

        public int RequesterId { get; set; }
        public string RequesterName { get; set; } = string.Empty;
        public string? RequesterProfilePic { get; set; }

        public int ReceiverId { get; set; }
        public string ReceiverName { get; set; } = string.Empty;
        public string? ReceiverProfilePic { get; set; }

        public string RequestDescription { get; set; } = string.Empty;
        public requestStatus Status { get; set; }
        public paymentStatus PaymentStatus { get; set; }
        public DateTime RequestDate { get; set; }
        public DateTime? Deadline { get; set; }

        public decimal TotalAmount { get; set; }
        public decimal ServiceTax { get; set; }

        public List<string> Tags { get; set; } = new();
        public List<RequestArtworkVM> Artworks { get; set; } = new();

        // helpers
        public bool IsCurrentUserArtist { get; set; }
        public bool IsCurrentUserRequester { get; set; }
        public bool IsCurrentUserReceiver { get; set; }
    }
}
