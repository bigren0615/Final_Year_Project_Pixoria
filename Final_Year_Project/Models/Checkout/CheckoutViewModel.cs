namespace Final_Year_Project.Models.Cart
{
    public class CheckoutViewModel
    {
        public string Username { get; set; } = "";
        public string PhoneNumber { get; set; } = "";
        public string Address { get; set; } = "";
        public List<CartViewModel> CartItems { get; set; } = new();
    }
}
