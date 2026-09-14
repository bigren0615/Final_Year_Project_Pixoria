using Final_Year_Project.Enums;
using Final_Year_Project.Models.DB;
using Final_Year_Project.Models.OrderHistory;
using Final_Year_Project.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Final_Year_Project.Controllers
{
    [Authorize(Policy = "NonAdminOnly")]
    [Route("Profile/OrderHistory")]
    public class OrderController : Controller
    {
        private readonly SupabaseService _supabaseService;
        private readonly EnumService _enumService;

        public OrderController(SupabaseService supabaseService, EnumService enumService)
        {
            _supabaseService = supabaseService;
            _enumService = enumService;
        }

        [HttpGet("")]
        public async Task<IActionResult> OrderHistory(string? order_status = null)
        {
            var client = _supabaseService.GetClient();

            var uidClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (uidClaim == null)
                return RedirectToAction("Login", "Account");

            int uid = int.Parse(uidClaim);

            var query = client.From<Order>()
                .Filter("UId", Supabase.Postgrest.Constants.Operator.Equals, uid);

            if (!string.IsNullOrEmpty(order_status))
            {
                if (Enum.TryParse<orderStatus>(order_status, true, out var parsedStatus))
                {
                    string lowercaseStatus = parsedStatus.ToString().ToLower();
                    query = query.Filter("paymentStatus", Supabase.Postgrest.Constants.Operator.Equals, lowercaseStatus);
                }
            }

            var orderResponse = await query.Order("OId", Supabase.Postgrest.Constants.Ordering.Descending).Get();
            var allOrders = orderResponse.Models;

            var pendingOrders = allOrders
                .Where(o => o.PaymentStatus?.ToLower() == "pending")
                .OrderByDescending(o => o.OrderDateTime)
                .ToList();

            var otherOrders = allOrders
                .Where(o => o.PaymentStatus?.ToLower() != "pending")
                .OrderByDescending(o => o.OrderDateTime)
                .ToList();

            var sortedOrders = pendingOrders.Concat(otherOrders).ToList();

            var orderItems = new Dictionary<string, List<OrderItem>>();
            foreach (var o in sortedOrders)
            {
                var itemsResponse = await client
                    .From<OrderItem>()
                    .Filter("OId", Supabase.Postgrest.Constants.Operator.Equals, o.OId)
                    .Get();

                orderItems[o.OId!] = itemsResponse.Models;
            }

            var viewModel = new OrderHistoryViewModel
            {
                Orders = sortedOrders,
                OrderItems = orderItems,
                SelectedStatus = order_status
            };

            return View(viewModel);
        }

        [HttpGet("OrderDetail")]
        public async Task<IActionResult> OrderDetailHistory(string OId)
        {
            if (string.IsNullOrEmpty(OId))
                return RedirectToAction("OrderHistory");

            var client = _supabaseService.GetClient();

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return RedirectToAction("Login", "Account");

            int uid = int.Parse(userId);

            var userResponse = await client.From<Users>()
                .Where(u => u.Uid == uid)
                .Single();

            var orderResponse = await client
                .From<Order>()
                .Select("*")
                .Filter("OId", Supabase.Postgrest.Constants.Operator.Equals, OId)
                .Filter("UId", Supabase.Postgrest.Constants.Operator.Equals, uid)
                .Single();

            var order = orderResponse;
            if (order == null)
                return RedirectToAction("OrderHistory");

            var itemsResponse = await client
                .From<OrderItem>()
                .Select("*")
                .Filter("OId", Supabase.Postgrest.Constants.Operator.Equals, OId)
                .Get();

            var items = itemsResponse.Models;

            var reviewResponse = await client
                .From<Review>()
                .Filter("Uid", Supabase.Postgrest.Constants.Operator.Equals, uid)
                .Get();

            var userReviews = reviewResponse.Models?.Select(r => r.PId).ToList() ?? new List<string>();

            ViewData["UserReviews"] = userReviews;

            // Get refund requests for this order
            var refundResponse = await client
                .From<Refund>()
                .Filter("OId", Supabase.Postgrest.Constants.Operator.Equals, OId)
                .Get();

            var refundRequests = refundResponse.Models?.ToDictionary(r => r.OrderItemId, r => r) ?? new Dictionary<long, Refund>();

            ViewData["RefundRequests"] = refundRequests;

            var viewModel = new OrderDetailViewModel
            {
                Order = order,
                Items = items.ToList(),
                Username = userResponse.Username
            };

            return View(viewModel);
        }

        [HttpPost("ConfirmDelivered")]
        public async Task<IActionResult> ConfirmDelivered(string OId)
        {
            if (string.IsNullOrEmpty(OId))
                return RedirectToAction("OrderHistory");

            var client = _supabaseService.GetClient();

            // Get the order
            var orderResponse = await client
                .From<Order>()
                .Filter("OId", Supabase.Postgrest.Constants.Operator.Equals, OId)
                .Single();

            if (orderResponse == null)
                return RedirectToAction("OrderHistory");

            // Update fields
            orderResponse.DeliveryStatus = _enumService.ToStringValue(deliveryStatus.delivered);
            orderResponse.OrderStatus = _enumService.ToStringValue(orderStatus.completed);
            orderResponse.DeliveryDateTime = DateTime.UtcNow;

            await orderResponse.Update<Order>();

            return RedirectToAction("OrderDetailHistory", new { OId = OId });
        }

        [HttpGet("ReviewProduct")]
        public async Task<IActionResult> ReviewProduct(string PId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return RedirectToAction("Login", "Account");

            var client = _supabaseService.GetClient();
            int uid = int.Parse(userId);

            var existingReviewResponse = await client
                .From<Review>()
                .Filter("Uid", Supabase.Postgrest.Constants.Operator.Equals, uid)
                .Filter("PId", Supabase.Postgrest.Constants.Operator.Equals, PId)
                .Get();

            if (existingReviewResponse.Models.Any())
            {
                TempData["ReviewError"] = "You have already reviewed this product.";
                return RedirectToAction("OrderHistory");
            }

            var product = await client
                .From<Product>()
                .Filter("PId", Supabase.Postgrest.Constants.Operator.Equals, PId)
                .Single();

            if (product == null)
                return RedirectToAction("OrderHistory");

            var productImagesResponse = await client.From<ProductImage>().Get();
            var productImages = productImagesResponse.Models;

            var primaryImage = productImages.FirstOrDefault(i => i.PId == PId && i.IsPrimary)?.ImagePath;

            var vm = new ReviewViewModel
            {
                PId = PId,
                ProductName = product.ProductName,
                ImagePath = primaryImage
            };

            return View("ReviewProduct", vm);
        }

        [HttpPost("SubmitReview")]
        public async Task<IActionResult> SubmitReview(string PId, int Star, string? Comment)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return RedirectToAction("Login", "Account");

            int uid = int.Parse(userId);

            var client = _supabaseService.GetClient();

            var review = new Review
            {
                UId = uid,
                PId = PId,
                Star = Star,
                Comment = Comment
            };

            await client.From<Review>().Insert(review);

            return RedirectToAction("OrderHistory");
        }


    }
}
