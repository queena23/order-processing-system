using Newtonsoft.Json;

namespace OrderApi;

public class Order
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonProperty("customerId")]
    public string CustomerId { get; set; } = string.Empty;

    [JsonProperty("items")]
    public List<OrderItem> Items { get; set; } = new();

    [JsonProperty("totalAmount")]
    public decimal TotalAmount { get; set; }

    [JsonProperty("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class OrderItem
{
    [JsonProperty("productName")]
    public string ProductName { get; set; } = string.Empty;

    [JsonProperty("quantity")]
    public int Quantity { get; set; }

    [JsonProperty("unitPrice")]
    public decimal UnitPrice { get; set; }
}