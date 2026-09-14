using Supabase.Postgrest.Attributes;
using Supabase.Postgrest.Models;

[Table("order")]
public class Order : BaseModel
{
    [PrimaryKey("OId", false)]
    [Column("OId")]
    public string? OId { get; set; }

    [Column("UId")]
    public int Uid { get; set; }

    [Column("orderDateTime")]
    public DateTime OrderDateTime { get; set; }

    [Column("paymentDateTime")]
    public DateTime? PaymentDateTime { get; set; }

    [Column("deliveryDateTime")]
    public DateTime? DeliveryDateTime { get; set; }

    [Column("paymentStatus")]
    public string PaymentStatus { get; set; } = string.Empty;

    [Column("deliveryStatus")]
    public string DeliveryStatus { get; set; } = string.Empty;

    [Column("orderStatus")]
    public string OrderStatus { get; set; } = string.Empty;

    [Column("totalAmount")]
    public float TotalAmount { get; set; }

    [Column("address")]
    public string? Address { get; set; }

    [Column("userVoucherId")]
    public int? UserVoucherId { get; set; }
}

[Table("orderItem")]
public class OrderItem : BaseModel
{
    [PrimaryKey("orderItemId")]
    public int? OrderItemId { get; set; }

    [Column("OId")]
    public string? OId { get; set; }

    [Column("PId")]
    public string? PId { get; set; }

    [Column("price")]
    public float Price { get; set; }

    [Column("unit")]
    public int Unit { get; set; }

    [Column("subtotal")]
    public float Subtotal { get; set; }

    [Column("artwork_name")]
    public string? ArtworkName { get; set; }

    [Column("image")]
    public string? Image { get; set; }

}
