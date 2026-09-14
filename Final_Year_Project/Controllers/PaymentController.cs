using Final_Year_Project.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Final_Year_Project.Models.DB;

[Authorize(Policy = "NonAdminOnly")]
public class PaymentController : Controller
{
    private readonly IPaymentService _paymentService;
    private readonly SupabaseService _supabaseService;

    public PaymentController(IPaymentService paymentService, SupabaseService supabaseService)
    {
        _paymentService = paymentService;
        _supabaseService = supabaseService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateCheckoutSession(decimal total)
    {
        var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(uidValue))
            return RedirectToAction("Login", "Account");

        var uid = int.Parse(uidValue);

        var address = await _paymentService.GetDefaultAddressAsync(uid);

        if (string.IsNullOrWhiteSpace(address))
        {
            TempData["Error"] = "You must add a shipping address before making a payment.";
            return RedirectToAction("Checkout", "Cart");
        }

        var domain = $"{Request.Scheme}://{Request.Host}/";
        var session = _paymentService.CreateCheckoutSession(total, domain);

        HttpContext.Session.SetString("OrderTotal", total.ToString());
        return Redirect(session.Url);
    }

    [HttpPost]
    public async Task<IActionResult> PayExistingOrder(string orderId, decimal total)
    {
        var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(uidValue))
            return RedirectToAction("Login", "Account");

        var uid = int.Parse(uidValue);

        // Verify the order belongs to this user
        var client = _supabaseService.GetClient();
        var orderResponse = await client
            .From<Order>()
            .Filter("OId", Supabase.Postgrest.Constants.Operator.Equals, orderId)
            .Filter("UId", Supabase.Postgrest.Constants.Operator.Equals, uid)
            .Single();

        if (orderResponse == null)
        {
            TempData["Error"] = "Order not found.";
            return RedirectToAction("OrderHistory", "Order");
        }

        // Check if order is still pending
        if (orderResponse.PaymentStatus?.ToLower() != "pending")
        {
            TempData["Error"] = "This order has already been paid or cancelled.";
            return RedirectToAction("OrderHistory", "Order");
        }

        var address = await _paymentService.GetDefaultAddressAsync(uid);

        if (string.IsNullOrWhiteSpace(address))
        {
            TempData["Error"] = "You must add a shipping address before making a payment.";
            return RedirectToAction("OrderHistory", "Order");
        }

        var domain = $"{Request.Scheme}://{Request.Host}/";
        var session = _paymentService.CreateCheckoutSession(
            total, 
            domain, 
            $"Payment/ExistingOrderSuccess?orderId={orderId}", 
            $"Payment/ExistingOrderCancel?orderId={orderId}"
        );

        HttpContext.Session.SetString("OrderId", orderId);
        HttpContext.Session.SetString("OrderTotal", total.ToString());
        
        return Redirect(session.Url);
    }

    public async Task<IActionResult> ExistingOrderSuccess(string orderId)
    {
        var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(uidValue))
            return RedirectToAction("Login", "Account");

        var uid = int.Parse(uidValue);

        var client = _supabaseService.GetClient();
        
        // Get the order
        var orderResponse = await client
            .From<Order>()
            .Filter("OId", Supabase.Postgrest.Constants.Operator.Equals, orderId)
            .Filter("UId", Supabase.Postgrest.Constants.Operator.Equals, uid)
            .Single();

        if (orderResponse == null)
        {
            TempData["Error"] = "Order not found.";
            return RedirectToAction("OrderHistory", "Order");
        }

        // Update order status to paid
        orderResponse.PaymentStatus = "paid";
        orderResponse.PaymentDateTime = DateTime.UtcNow;
        orderResponse.OrderStatus = "pending";
        orderResponse.DeliveryStatus = "pending";

        await orderResponse.Update<Order>();

        // Clear session
        HttpContext.Session.Remove("OrderId");
        HttpContext.Session.Remove("OrderTotal");

        TempData["Success"] = "Payment successful! Your order has been confirmed.";
        return RedirectToAction("OrderDetailHistory", "Order", new { OId = orderId });
    }

    public async Task<IActionResult> ExistingOrderCancel(string orderId)
    {
        // Clear session
        HttpContext.Session.Remove("OrderId");
        HttpContext.Session.Remove("OrderTotal");

        TempData["Info"] = "Payment was cancelled. Your order is still pending.";
        return RedirectToAction("OrderDetailHistory", "Order", new { OId = orderId });
    }

    public async Task<IActionResult> Success()
    {
        var total = Convert.ToDecimal(HttpContext.Session.GetString("OrderTotal"));
        var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(uidValue))
            return RedirectToAction("Login", "Account");

        var uid = int.Parse(uidValue!);
        var createdOrder = await _paymentService.CreateOrderFromCartAsync(uid, total, "paid", "pending", "pending", true, DateTime.UtcNow);

        return View(createdOrder);
    }

    public async Task<IActionResult> Cancel()
    {
        var total = Convert.ToDecimal(HttpContext.Session.GetString("OrderTotal"));
        var uidValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(uidValue))
            return RedirectToAction("Login", "Account");

        var uid = int.Parse(uidValue!);
        var createdOrder = await _paymentService.CreateOrderFromCartAsync(uid, total, "pending", "pending", "pending", true, DateTime.UtcNow);

        return View(createdOrder);
    }
}
    