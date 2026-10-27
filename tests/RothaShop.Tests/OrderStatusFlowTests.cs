using RothaShop.Models;
using RothaShop.Services;

namespace RothaShop.Tests;

public class OrderStatusFlowTests
{
    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Pending, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Processing)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Processing, OrderStatus.Shipped)]
    [InlineData(OrderStatus.Processing, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Delivered)]
    public void CanTransition_AllowsTheDocumentedPath(OrderStatus current, OrderStatus next)
    {
        Assert.True(OrderStatusFlow.CanTransition(current, next));
    }

    [Theory]
    [InlineData(OrderStatus.Pending, OrderStatus.Shipped)]
    [InlineData(OrderStatus.Pending, OrderStatus.Delivered)]
    [InlineData(OrderStatus.Confirmed, OrderStatus.Delivered)]
    [InlineData(OrderStatus.Shipped, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Delivered, OrderStatus.Pending)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Pending)]
    [InlineData(OrderStatus.Processing, OrderStatus.Processing)]
    public void CanTransition_RejectsSkippedOrBackwardMoves(OrderStatus current, OrderStatus next)
    {
        Assert.False(OrderStatusFlow.CanTransition(current, next));
    }

    [Fact]
    public void Next_ReturnsEmptyForTerminalStatuses()
    {
        Assert.Empty(OrderStatusFlow.Next(OrderStatus.Delivered));
        Assert.Empty(OrderStatusFlow.Next(OrderStatus.Cancelled));
    }

    [Theory]
    [InlineData(OrderStatus.Pending)]
    [InlineData(OrderStatus.Confirmed)]
    [InlineData(OrderStatus.Processing)]
    [InlineData(OrderStatus.Shipped)]
    public void Next_OffersAtMostTwoChoicesOnTheHappyPath(OrderStatus current)
    {
        Assert.InRange(OrderStatusFlow.Next(current).Count, 1, 2);
    }
}
