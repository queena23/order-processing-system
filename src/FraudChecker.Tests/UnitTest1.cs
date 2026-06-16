using FraudChecker;
using Xunit;

namespace FraudChecker.Tests;

public class FraudRulesTests
{
    [Fact]
    public void FlaggedCustomer_FailsCheck()
    {
        var order = new Order
        {
            CustomerId = "flagged-001",
            TotalAmount = 50,
            Items = new List<OrderItem>
            {
                new OrderItem { ProductName = "Book", Quantity = 1, UnitPrice = 50 }
            }
        };

        var (passed, reason) = FraudCheck.RunFraudRules(order);

        Assert.False(passed);
        Assert.Equal("Customer account flagged for suspicious activity", reason);
    }

    [Fact]
    public void HighValueSingleItem_FailsCheck()
    {
        var order = new Order
        {
            CustomerId = "customer-123",
            TotalAmount = 999.99m,
            Items = new List<OrderItem>
            {
                new OrderItem { ProductName = "Laptop", Quantity = 1, UnitPrice = 999.99m }
            }
        };

        var (passed, reason) = FraudCheck.RunFraudRules(order);

        Assert.False(passed);
        Assert.Equal("High value single item order requires manual review", reason);
    }

    [Fact]
    public void UnrealisticQuantity_FailsCheck()
    {
        var order = new Order
        {
            CustomerId = "customer-123",
            TotalAmount = 100,
            Items = new List<OrderItem>
            {
                new OrderItem { ProductName = "Pen", Quantity = 100, UnitPrice = 1 }
            }
        };

        var (passed, reason) = FraudCheck.RunFraudRules(order);

        Assert.False(passed);
        Assert.Equal("Unusually high quantity detected", reason);
    }

    [Fact]
    public void NegativeTotal_FailsCheck()
    {
        var order = new Order
        {
            CustomerId = "customer-123",
            TotalAmount = -10,
            Items = new List<OrderItem>
            {
                new OrderItem { ProductName = "Refund", Quantity = 1, UnitPrice = -10 }
            }
        };

        var (passed, reason) = FraudCheck.RunFraudRules(order);

        Assert.False(passed);
        Assert.Equal("Invalid order total", reason);
    }

    [Fact]
    public void LegitimateOrder_PassesCheck()
    {
        var order = new Order
        {
            CustomerId = "customer-123",
            TotalAmount = 45.00m,
            Items = new List<OrderItem>
            {
                new OrderItem { ProductName = "Book", Quantity = 2, UnitPrice = 15.00m },
                new OrderItem { ProductName = "Pen", Quantity = 3, UnitPrice = 5.00m }
            }
        };

        var (passed, reason) = FraudCheck.RunFraudRules(order);

        Assert.True(passed);
        Assert.Null(reason);
    }
}