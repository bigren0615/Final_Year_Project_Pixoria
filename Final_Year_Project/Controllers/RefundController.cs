using Final_Year_Project.Enums;
using Final_Year_Project.Models.DB;
using Final_Year_Project.Models.Refund;
using Final_Year_Project.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Final_Year_Project.Controllers
{
    [Authorize(Policy = "NonAdminOnly")]
    public class RefundController : Controller
    {
        private readonly SupabaseService _supabaseService;
        private readonly EnumService _enumService;
        private readonly LocalStorageService _storageService;

        public RefundController(SupabaseService supabaseService, EnumService enumService, LocalStorageService storageService)
        {
            _supabaseService = supabaseService;
            _enumService = enumService;
            _storageService = storageService;
        }

        [HttpGet]
        public async Task<IActionResult> RequestRefund(int orderItemId)
        {
            if (orderItemId <= 0)
                return RedirectToAction("OrderHistory", "Order");

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return RedirectToAction("Login", "Account");

            int uid = int.Parse(userId);
            var client = _supabaseService.GetClient();

            // Get order item
            var orderItemResponse = await client
                .From<OrderItem>()
                .Filter("orderItemId", Supabase.Postgrest.Constants.Operator.Equals, orderItemId)
                .Single();

            if (orderItemResponse == null)
                return RedirectToAction("OrderHistory", "Order");

            // Get order to verify ownership and delivery status
            var orderResponse = await client
                .From<Order>()
                .Filter("OId", Supabase.Postgrest.Constants.Operator.Equals, orderItemResponse.OId)
                .Filter("UId", Supabase.Postgrest.Constants.Operator.Equals, uid)
                .Single();

            if (orderResponse == null)
                return RedirectToAction("OrderHistory", "Order");

            // Check if already has a refund request
            var existingRefundResponse = await client
                .From<Refund>()
                .Filter("orderItemId", Supabase.Postgrest.Constants.Operator.Equals, orderItemId)
                .Get();

            if (existingRefundResponse.Models.Any())
            {
                TempData["RefundError"] = "A refund request already exists for this item.";
                return RedirectToAction("OrderDetailHistory", "Order", new { OId = orderResponse.OId });
            }

            // Calculate refund due date (7 days after delivery)
            DateTime? refundDueDate = null;
            bool isRefundable = false;

            if (orderResponse.DeliveryDateTime.HasValue)
            {
                refundDueDate = orderResponse.DeliveryDateTime.Value.AddDays(7);
                isRefundable = DateTime.UtcNow <= refundDueDate;
            }

            var viewModel = new RefundRequestViewModel
            {
                OrderId = orderResponse.OId,
                OrderItemId = orderItemId,
                ProductName = orderItemResponse.ArtworkName,
                ProductImage = orderItemResponse.Image,
                Price = orderItemResponse.Price,
                Quantity = orderItemResponse.Unit,
                DeliveryDate = orderResponse.DeliveryDateTime,
                RefundDueDate = refundDueDate,
                IsRefundable = isRefundable
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitRefund(RefundRequestViewModel model)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return RedirectToAction("Login", "Account");

            int uid = int.Parse(userId);
            var client = _supabaseService.GetClient();

            if (!ModelState.IsValid)
            {
                return View("RequestRefund", model);
            }

            // Verify order item and ownership
            var orderItemResponse = await client
                .From<OrderItem>()
                .Filter("orderItemId", Supabase.Postgrest.Constants.Operator.Equals, model.OrderItemId)
                .Single();

            if (orderItemResponse == null)
            {
                TempData["RefundError"] = "Invalid order item.";
                return RedirectToAction("OrderHistory", "Order");
            }

            var orderResponse = await client
                .From<Order>()
                .Filter("OId", Supabase.Postgrest.Constants.Operator.Equals, orderItemResponse.OId)
                .Filter("UId", Supabase.Postgrest.Constants.Operator.Equals, uid)
                .Single();

            if (orderResponse == null || !orderResponse.DeliveryDateTime.HasValue)
            {
                TempData["RefundError"] = "Invalid order or order not yet delivered.";
                return RedirectToAction("OrderHistory", "Order");
            }

            // Check refund deadline
            var refundDueDate = orderResponse.DeliveryDateTime.Value.AddDays(7);
            if (DateTime.UtcNow > refundDueDate)
            {
                TempData["RefundError"] = "Refund deadline has passed.";
                return RedirectToAction("OrderDetailHistory", "Order", new { OId = orderResponse.OId });
            }

            // Create refund request
            var refund = new Refund
            {
                OrderItemId = model.OrderItemId,
                OId = orderResponse.OId,
                UId = uid,
                Reason = model.Reason,
                RefundStatus = _enumService.ToStringValue(refundStatus.pending),
                RequestDate = DateTime.UtcNow,
                DueDate = refundDueDate,
                CreatedAt = DateTime.UtcNow
            };

            var refundResponse = await client.From<Refund>().Insert(refund);
            if (refundResponse.Models == null || !refundResponse.Models.Any())
            {
                TempData["RefundError"] = "Failed to create refund request.";
                return View("RequestRefund", model);
            }

            var newRefundId = refundResponse.Models.First().RefundId;

            // Upload images if provided
            if (model.Images != null && model.Images.Any())
            {
                foreach (var image in model.Images)
                {
                    var savedFileName = await _storageService.SaveAsync(
                        image,
                        "refund",
                        LocalStorageService.DefaultAllowedContentTypes
                    );

                    if (savedFileName == null)
                        continue;

                    var imageUrl = _storageService.BuildFileUrl("refund", savedFileName);

                    var refundImage = new RefundImage
                    {
                        RefundId = newRefundId,
                        ImagePath = imageUrl,
                        UploadedAt = DateTime.UtcNow
                    };

                    await client.From<RefundImage>().Insert(refundImage);
                }
            }

            TempData["RefundSuccess"] = "Refund request submitted successfully!";
            return RedirectToAction("OrderDetailHistory", "Order", new { OId = orderResponse.OId });
        }
    }
}
