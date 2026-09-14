using System.ComponentModel.DataAnnotations;

namespace Final_Year_Project.Enums
{
    public enum requestStatus
    {
        [Display(Name = "Pending Payment")]
        pending,

        [Display(Name = "Pending Approval")]
        requested,

        [Display(Name = "In Progress")]
        approved,

        [Display(Name = "Rejected")]
        rejected,

        [Display(Name = "Draft Sent")]
        drafted,

        [Display(Name = "Draft Approved")]
        draft_approved,

        [Display(Name = "Completed")]
        completed,

        [Display(Name = "Cancelled")]
        cancelled
    }
}
