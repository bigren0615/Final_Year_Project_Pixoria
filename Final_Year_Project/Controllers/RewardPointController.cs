using Final_Year_Project.Models.DB;
using Final_Year_Project.Models.RewardPoint;
using Final_Year_Project.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Final_Year_Project.Controllers
{
    [Authorize(Policy = "NonAdminOnly")]
    public class RewardPointController : Controller
    {
        private readonly SupabaseService _supabaseService;
        private const int DAILY_REWARD_POINTS = 10;

        public RewardPointController(SupabaseService supabaseService)
        {
            _supabaseService = supabaseService;
        }

        [Authorize]
        public async Task<IActionResult> DailyLogin()
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uid) || !int.TryParse(uid, out var uidInt))
                return RedirectToAction("Login", "Account");

            var client = _supabaseService.GetClient();

            // Get user's current reward points
            var userResponse = await client
                .From<Users>()
                .Filter("UId", Supabase.Postgrest.Constants.Operator.Equals, uidInt)
                .Get();

            var user = userResponse.Models.FirstOrDefault();
            if (user == null)
                return RedirectToAction("Index", "Home");

            // Check if user has already claimed today's reward
            var hasClaimedToday = await HasClaimedDailyLoginTodayAsync(uidInt);

            // Get all point history for this user (sorted by most recent first)
            var historyResponse = await client
                .From<PointLog>()
                .Filter("UId", Supabase.Postgrest.Constants.Operator.Equals, uidInt)
                .Order("created_at", Supabase.Postgrest.Constants.Ordering.Descending)
                .Get();

            var viewModel = new DailyLoginViewModel
            {
                HasClaimedToday = hasClaimedToday,
                TotalRewardPoints = user.RewardPoint ?? 0,
                DailyRewardPoints = DAILY_REWARD_POINTS,
                PointHistory = historyResponse.Models
            };

            return View(viewModel);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClaimDailyLogin()
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uid) || !int.TryParse(uid, out var uidInt))
                return RedirectToAction("Login", "Account");

            var client = _supabaseService.GetClient();

            // Get user
            var userResponse = await client
                .From<Users>()
                .Filter("UId", Supabase.Postgrest.Constants.Operator.Equals, uidInt)
                .Get();

            var user = userResponse.Models.FirstOrDefault();
            if (user == null)
            {
                TempData["AlertClass"] = "alert-error";
                TempData["Info"] = "User not found.";
                return RedirectToAction("DailyLogin");
            }

            // Check if user has already claimed today's reward
            if (await HasClaimedDailyLoginTodayAsync(uidInt))
            {
                TempData["AlertClass"] = "alert-warning";
                TempData["Info"] = "You have already claimed today's reward.";
                return RedirectToAction("DailyLogin");
            }

            // Create point log entry first to ensure we have a record
            var pointLog = new PointLog
            {
                UId = uidInt,
                Point = DAILY_REWARD_POINTS,
                Remark = "daily login",
                CreatedAt = DateTime.UtcNow
            };
            await client.From<PointLog>().Insert(pointLog);

            // Add points to user
            user.RewardPoint = (user.RewardPoint ?? 0) + DAILY_REWARD_POINTS;
            await client.From<Users>().Update(user);

            TempData["AlertClass"] = "alert-success";
            TempData["Info"] = $"Successfully claimed {DAILY_REWARD_POINTS} reward points!";
            return RedirectToAction("DailyLogin");
        }

        private async Task<bool> HasClaimedDailyLoginTodayAsync(int userId)
        {
            var client = _supabaseService.GetClient();
            var today = DateTime.UtcNow.Date;
            var tomorrow = today.AddDays(1);

            var todayLogsResponse = await client
                .From<PointLog>()
                .Filter("UId", Supabase.Postgrest.Constants.Operator.Equals, userId)
                .Filter("remark", Supabase.Postgrest.Constants.Operator.Equals, "daily login")
                .Filter("created_at", Supabase.Postgrest.Constants.Operator.GreaterThanOrEqual, today.ToString("yyyy-MM-dd"))
                .Filter("created_at", Supabase.Postgrest.Constants.Operator.LessThan, tomorrow.ToString("yyyy-MM-dd"))
                .Get();

            return todayLogsResponse.Models.Any();
        }

        private const int VOUCHER_POINT_COST = 500;

        private static List<VoucherItem> GetAvailableVouchers()
        {
            return new List<VoucherItem>
            {
                new VoucherItem
                {
                    VoucherId = 1,
                    VoucherName = "Discount Shipping Voucher",
                    Description = "Get free shipping on your next artwork purchase. Perfect for fans ordering physical prints!",
                    PointCost = VOUCHER_POINT_COST,
                    VoucherType = "shipping"
                },
                new VoucherItem
                {
                    VoucherId = 2,
                    VoucherName = "Discount Product Voucher",
                    Description = "Get a discount on any physical artwork. A perfect way to support your favorite artist.",
                    PointCost = VOUCHER_POINT_COST,
                    VoucherType = "product"
                }
            };
        }

        [Authorize]
        public async Task<IActionResult> VoucherRedemption()
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uid) || !int.TryParse(uid, out var uidInt))
                return RedirectToAction("Login", "Account");

            var client = _supabaseService.GetClient();

            // Get user's current reward points
            var userResponse = await client
                .From<Users>()
                .Filter("UId", Supabase.Postgrest.Constants.Operator.Equals, uidInt)
                .Get();

            var user = userResponse.Models.FirstOrDefault();
            if (user == null)
                return RedirectToAction("Index", "Home");

            // Get the two available vouchers (Id 1: shipping, Id 2: product)
            var availableVouchers = GetAvailableVouchers();

            // Get user's active vouchers (status = "active")
            var userVouchersResponse = await client
                .From<UserVoucher>()
                .Filter("UId", Supabase.Postgrest.Constants.Operator.Equals, uidInt)
                .Filter("status", Supabase.Postgrest.Constants.Operator.Equals, "active")
                .Order("created_at", Supabase.Postgrest.Constants.Ordering.Descending)
                .Get();

            var activeUserVouchers = new List<UserVoucherItem>();
            foreach (var uv in userVouchersResponse.Models)
            {
                var voucher = availableVouchers.FirstOrDefault(v => v.VoucherId == uv.VoucherId);
                if (voucher != null)
                {
                    activeUserVouchers.Add(new UserVoucherItem
                    {
                        UserVoucherId = uv.UserVoucherId,
                        VoucherId = uv.VoucherId,
                        VoucherName = voucher.VoucherName,
                        Description = voucher.Description,
                        VoucherType = voucher.VoucherType,
                        Status = uv.Status,
                        CreatedAt = uv.CreatedAt
                    });
                }
            }

            var viewModel = new VoucherRedemptionViewModel
            {
                TotalRewardPoints = user.RewardPoint ?? 0,
                AvailableVouchers = availableVouchers,
                ActiveUserVouchers = activeUserVouchers
            };

            return View(viewModel);
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RedeemVoucher(int voucherId)
        {
            var uid = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(uid) || !int.TryParse(uid, out var uidInt))
                return RedirectToAction("Login", "Account");

            var client = _supabaseService.GetClient();

            // Get voucher from available vouchers
            var availableVouchers = GetAvailableVouchers();
            var selectedVoucher = availableVouchers.FirstOrDefault(v => v.VoucherId == voucherId);
            
            if (selectedVoucher == null)
            {
                TempData["AlertClass"] = "alert-error";
                TempData["Info"] = "Invalid voucher selected.";
                return RedirectToAction("VoucherRedemption");
            }

            // Get user
            var userResponse = await client
                .From<Users>()
                .Filter("UId", Supabase.Postgrest.Constants.Operator.Equals, uidInt)
                .Get();

            var user = userResponse.Models.FirstOrDefault();
            if (user == null)
            {
                TempData["AlertClass"] = "alert-error";
                TempData["Info"] = "User not found.";
                return RedirectToAction("VoucherRedemption");
            }

            // Check if user has enough points
            var currentPoints = user.RewardPoint ?? 0;
            if (currentPoints < VOUCHER_POINT_COST)
            {
                TempData["AlertClass"] = "alert-error";
                TempData["Info"] = $"Insufficient points. You need {VOUCHER_POINT_COST} points but only have {currentPoints} points.";
                return RedirectToAction("VoucherRedemption");
            }

            // Create user voucher entry first (most important - user gets the voucher)
            var userVoucher = new UserVoucher
            {
                UId = uidInt,
                VoucherId = voucherId,
                Status = "active",
                CreatedAt = DateTime.UtcNow
            };
            await client.From<UserVoucher>().Insert(userVoucher);

            // Create point log entry for the deduction (for tracking)
            var pointLog = new PointLog
            {
                UId = uidInt,
                Point = -VOUCHER_POINT_COST,
                Remark = $"exchange voucher: {selectedVoucher.VoucherName}",
                CreatedAt = DateTime.UtcNow
            };
            await client.From<PointLog>().Insert(pointLog);

            // Deduct points from user (last - in case of failure, voucher is already assigned)
            user.RewardPoint = currentPoints - VOUCHER_POINT_COST;
            await client.From<Users>().Update(user);

            TempData["AlertClass"] = "alert-success";
            TempData["Info"] = $"Successfully redeemed {selectedVoucher.VoucherName}! {VOUCHER_POINT_COST} points have been deducted.";
            return RedirectToAction("VoucherRedemption");
        }

        public IActionResult Tracking()
        {
            return View();
        }
    }
}
