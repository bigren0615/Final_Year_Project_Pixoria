namespace Final_Year_Project.Models.CommissionPlans
{
    public class CommissionPlanRequestVM
    {
        public CommissionPlanViewModel Plan { get; set; } = new();
        public RequestAddEditVM Request { get; set; } = new();
    }
}
