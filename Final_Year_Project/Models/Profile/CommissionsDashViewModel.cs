using System.Collections.Generic;

namespace Final_Year_Project.Models.Profile
{
    public class RequestSummaryViewModel
    {
        public int RId { get; set; }
        public int CId { get; set; }
        public string ClientName { get; set; } = "";
        public DateTime RequestDate { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal ServiceTax { get; set; }
        public string PlanTitle { get; set; } = "";
    }

    public class CommissionsDashViewModel
    {
        // Completed requests (most recent first)
        public List<RequestSummaryViewModel>? CompletedRequests { get; set; }

        // Chart data keyed by period (e.g. period30, period90, period365, periodall)
        // Each value will be an object with labels and values arrays
        public Dictionary<string, object>? ChartData { get; set; }
    }
}
