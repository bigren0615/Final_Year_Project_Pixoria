using Final_Year_Project.Models.DB;

namespace Final_Year_Project.Models.RewardPoint
{
    public class VoucherRedemptionViewModel
    {
        public int TotalRewardPoints { get; set; }
        public List<VoucherItem> AvailableVouchers { get; set; } = new List<VoucherItem>();
        public List<UserVoucherItem> ActiveUserVouchers { get; set; } = new List<UserVoucherItem>();
    }

    public class VoucherItem
    {
        public int VoucherId { get; set; }
        public string VoucherName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int PointCost { get; set; }
        public string VoucherType { get; set; } = string.Empty; // "shipping" or "product"
    }

    public class UserVoucherItem
    {
        public int UserVoucherId { get; set; }
        public int VoucherId { get; set; }
        public string VoucherName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string VoucherType { get; set; } = string.Empty; // "shipping" or "product"
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
    }
}
