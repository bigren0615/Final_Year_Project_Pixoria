using Stripe.Checkout;
using Final_Year_Project.Models.DB;

namespace Final_Year_Project.Services
{
    public interface IPaymentService
    {
        Session CreateCheckoutSession(decimal total, string domain, string successUrl = "Payment/Success", string cancelUrl = "Payment/Cancel");
        Task<Order?> CreateOrderFromCartAsync(int uid, decimal total, string paymentStatus, string orderStatus, string deliverytStatus, bool reduceStock = true, DateTime? paymentDateTime = null);
        Task<string> GetDefaultAddressAsync(int uid);
    }
}
