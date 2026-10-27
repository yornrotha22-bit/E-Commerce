using System.Globalization;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using RothaShop.Data;
using RothaShop.Localization;
using RothaShop.Models;
using Microsoft.Extensions.Configuration;

namespace RothaShop.Services;

public class PlaceOrderCommand
{
    public string CustomerName { get; set; } = string.Empty;

    public string CustomerPhone { get; set; } = string.Empty;

    public string? CustomerEmail { get; set; }

    public string AddressLine1 { get; set; } = string.Empty;

    public string? AddressLine2 { get; set; }

    public string City { get; set; } = string.Empty;

    public string? State { get; set; }

    public string? PostalCode { get; set; }

    public string Country { get; set; } = "Cambodia";

    public string? Notes { get; set; }

    public string? CouponCode { get; set; }

    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.CashOnDelivery;

    public bool SaveAddress { get; set; }
}

public class PlaceOrderResult
{
    public bool Success { get; init; }

    public int OrderId { get; init; }

    public string? OrderNumber { get; init; }

    public string Message { get; init; } = string.Empty;

    public string? ErrorField { get; init; }

    public static PlaceOrderResult Ok(int orderId, string orderNumber, string? message = null) => new()
    {
        Success = true,
        OrderId = orderId,
        OrderNumber = orderNumber,
        Message = message ?? $"Order {orderNumber} was placed."
    };

    public static PlaceOrderResult Failure(string message, string? errorField = null) => new()
    {
        Success = false,
        Message = message,
        ErrorField = errorField
    };
}

public class CouponApplication
{
    public bool IsValid { get; init; }

    public string? Code { get; init; }

    public string? Message { get; init; }

    public decimal DiscountAmount { get; init; }

    public static CouponApplication None() => new() { IsValid = false };

    public static CouponApplication Invalid(string message) => new()
    {
        IsValid = false,
        Message = message
    };

    public static CouponApplication Valid(string code, decimal discount, string? message = null) => new()
    {
        IsValid = true,
        Code = code,
        DiscountAmount = discount,
        Message = message ?? $"Coupon {code} applied: -${discount:N2}."
    };
}

public interface IOrderService
{
    Task<CouponApplication> ValidateCouponAsync(
        string? code,
        decimal subtotal,
        CancellationToken cancellationToken = default);

    Task<PlaceOrderResult> PlaceOrderAsync(
        string userId,
        PlaceOrderCommand command,
        CancellationToken cancellationToken = default);

    Task<PlaceOrderResult> CancelOrderAsync(
        int orderId,
        string userId,
        CancellationToken cancellationToken = default);

    Task<PlaceOrderResult> CancelOrderByStaffAsync(
        int orderId,
        string? staffUserId,
        string reason,
        CancellationToken cancellationToken = default);

    Task<OrderStatusUpdateResult> UpdateStatusAsync(
        int orderId,
        OrderStatus newStatus,
        string? carrier,
        string? trackingNumber,
        string? note,
        string? staffUserId,
        CancellationToken cancellationToken = default);

    Task<OrderStatusUpdateResult> UpdateShippingAsync(
        int orderId,
        string? carrier,
        string? trackingNumber,
        string? staffUserId,
        CancellationToken cancellationToken = default);
}

public class OrderStatusUpdateResult
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    public static OrderStatusUpdateResult Ok(string message) => new() { Success = true, Message = message };

    public static OrderStatusUpdateResult Fail(string message) => new() { Success = false, Message = message };
}

/// <summary>
/// Allowed order status transitions. Cancelling is handled separately because
/// it must restore stock and coupon usage.
/// </summary>
public static class OrderStatusFlow
{
    public static IReadOnlyList<OrderStatus> Next(OrderStatus current) => current switch
    {
        OrderStatus.Pending => new[] { OrderStatus.Confirmed, OrderStatus.Cancelled },
        OrderStatus.Confirmed => new[] { OrderStatus.Processing, OrderStatus.Cancelled },
        OrderStatus.Processing => new[] { OrderStatus.Shipped, OrderStatus.Cancelled },
        OrderStatus.Shipped => new[] { OrderStatus.Delivered },
        _ => Array.Empty<OrderStatus>()
    };

    public static bool CanTransition(OrderStatus current, OrderStatus next) =>
        Next(current).Contains(next);
}

/// <summary>
/// Creates and cancels customer orders. Prices, stock, shipping and discounts
/// are always recalculated on the server inside a database transaction.
/// </summary>
public class OrderService : IOrderService
{
    private const int MaxAddressesPerCustomer = 10;

    private readonly ApplicationDbContext _dbContext;
    private readonly IPaymentService _paymentService;
    private readonly ITranslator _t;
    private readonly ILogger<OrderService> _logger;
    private readonly IEmailSender _emailSender;
    private readonly IConfiguration _configuration;

    public OrderService(
        ApplicationDbContext dbContext,
        IPaymentService paymentService,
        ITranslator translator,
        IEmailSender emailSender,
        IConfiguration configuration,
        ILogger<OrderService> logger)
    {
        _dbContext = dbContext;
        _paymentService = paymentService;
        _t = translator;
        _emailSender = emailSender;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<CouponApplication> ValidateCouponAsync(
        string? code,
        decimal subtotal,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return CouponApplication.None();
        }

        var normalized = code.Trim().ToUpperInvariant();

        var coupon = await _dbContext.Coupons.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Code == normalized, cancellationToken);

        if (coupon is null)
        {
            return CouponApplication.Invalid(_t["msg.couponNotFound"]);
        }

        var error = CheckCoupon(coupon, subtotal, DateTime.UtcNow);

        return error is null
            ? CouponApplication.Valid(
                coupon.Code,
                CalculateDiscount(coupon, subtotal),
                _t.Format("msg.couponApplied", coupon.Code, FormatMoney(CalculateDiscount(coupon, subtotal))))
            : CouponApplication.Invalid(error);
    }

    public async Task<PlaceOrderResult> PlaceOrderAsync(
        string userId,
        PlaceOrderCommand command,
        CancellationToken cancellationToken = default)
    {
        // EnableRetryOnFailure requires user transactions to run inside the
        // execution strategy so a transient failure can be retried safely.
        var strategy = _dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            _dbContext.ChangeTracker.Clear();
            return await PlaceOrderInternalAsync(userId, command, cancellationToken);
        });
    }

    private async Task<PlaceOrderResult> PlaceOrderInternalAsync(
        string userId,
        PlaceOrderCommand command,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var cartItems = await _dbContext.CartItems
                .Include(c => c.ProductVariant)
                    .ThenInclude(v => v!.Product)
                        .ThenInclude(p => p!.Images)
                .Where(c => c.UserId == userId)
                .ToListAsync(cancellationToken);

            if (cartItems.Count == 0)
            {
                return PlaceOrderResult.Failure(_t["msg.cartEmpty"]);
            }

            foreach (var item in cartItems)
            {
                var variant = item.ProductVariant;

                if (variant?.Product is null || !variant.IsActive || !variant.Product.IsActive)
                {
                    return PlaceOrderResult.Failure(_t["msg.cartItemUnavailable"]);
                }

                if (item.Quantity < 1)
                {
                    return PlaceOrderResult.Failure(_t["msg.cartQuantityInvalid"]);
                }

                if (variant.StockQuantity < item.Quantity)
                {
                    return PlaceOrderResult.Failure(
                        _t.Format("msg.insufficientStockFor", variant.StockQuantity, variant.Product.Name));
                }
            }

            var paymentProvider = _paymentService.GetProvider(command.PaymentMethod);

            if (!paymentProvider.IsAvailable)
            {
                return PlaceOrderResult.Failure(
                    paymentProvider.UnavailableReason ?? _t["msg.paymentUnavailable"],
                    "PaymentMethod");
            }

            var settings = await _dbContext.StoreSettings.AsNoTracking()
                .Where(s => s.Key == "DefaultShippingFee"
                            || s.Key == "FreeShippingThreshold"
                            || s.Key == "Currency")
                .ToDictionaryAsync(s => s.Key, s => s.Value, cancellationToken);

            var defaultShippingFee = ReadDecimal(settings, "DefaultShippingFee", 2m);
            var freeShippingThreshold = ReadDecimal(settings, "FreeShippingThreshold", 50m);
            var currency = string.IsNullOrWhiteSpace(ReadString(settings, "Currency"))
                ? "USD"
                : ReadString(settings, "Currency")!;

            var subtotal = Math.Round(
                cartItems.Sum(i => i.ProductVariant!.Price * i.Quantity),
                2,
                MidpointRounding.AwayFromZero);

            Coupon? coupon = null;
            var discount = 0m;

            if (!string.IsNullOrWhiteSpace(command.CouponCode))
            {
                var normalized = command.CouponCode.Trim().ToUpperInvariant();
                coupon = await _dbContext.Coupons
                    .FirstOrDefaultAsync(c => c.Code == normalized, cancellationToken);

                if (coupon is null)
                {
                    return PlaceOrderResult.Failure(_t["msg.couponNotFound"]);
                }

                var couponError = CheckCoupon(coupon, subtotal, DateTime.UtcNow);

                if (couponError is not null)
                {
                    return PlaceOrderResult.Failure(couponError);
                }

                discount = CalculateDiscount(coupon, subtotal);
            }

            var shippingFee = subtotal >= freeShippingThreshold ? 0m : defaultShippingFee;
            var total = Math.Max(0m, subtotal + shippingFee - discount);

            var orderNumber = await GenerateOrderNumberAsync(cancellationToken);
            var now = DateTime.UtcNow;

            var order = new Order
            {
                OrderNumber = orderNumber,
                UserId = userId,
                CustomerName = command.CustomerName.Trim(),
                CustomerPhone = command.CustomerPhone.Trim(),
                CustomerEmail = string.IsNullOrWhiteSpace(command.CustomerEmail)
                    ? null
                    : command.CustomerEmail.Trim(),
                ShippingAddressLine1 = command.AddressLine1.Trim(),
                ShippingAddressLine2 = string.IsNullOrWhiteSpace(command.AddressLine2)
                    ? null
                    : command.AddressLine2.Trim(),
                ShippingCity = command.City.Trim(),
                ShippingState = string.IsNullOrWhiteSpace(command.State) ? null : command.State.Trim(),
                ShippingPostalCode = string.IsNullOrWhiteSpace(command.PostalCode)
                    ? null
                    : command.PostalCode.Trim(),
                ShippingCountry = command.Country.Trim(),
                Notes = string.IsNullOrWhiteSpace(command.Notes) ? null : command.Notes.Trim(),
                Subtotal = subtotal,
                ShippingFee = shippingFee,
                DiscountAmount = discount,
                TotalAmount = total,
                CouponId = coupon?.Id,
                CouponCode = coupon?.Code,
                PaymentMethod = command.PaymentMethod,
                PaymentStatus = PaymentStatus.Pending,
                OrderStatus = OrderStatus.Pending,
                CreatedAt = now,
                UpdatedAt = now
            };

            foreach (var item in cartItems)
            {
                var variant = item.ProductVariant!;
                var product = variant.Product!;

                order.Items.Add(new OrderItem
                {
                    ProductId = product.Id,
                    ProductVariantId = variant.Id,
                    ProductName = product.Name,
                    SKU = variant.SKU,
                    VariantDescription = BuildVariantDescription(variant),
                    ImageUrl = product.MainImageUrl
                        ?? product.Images.OrderBy(i => i.DisplayOrder).Select(i => i.Url).FirstOrDefault(),
                    UnitPrice = variant.Price,
                    Quantity = item.Quantity,
                    LineTotal = Math.Round(
                        variant.Price * item.Quantity,
                        2,
                        MidpointRounding.AwayFromZero)
                });

                variant.StockQuantity -= item.Quantity;

                _dbContext.StockMovements.Add(new StockMovement
                {
                    ProductVariant = variant,
                    QuantityChange = -item.Quantity,
                    StockAfter = variant.StockQuantity,
                    Type = StockMovementType.Outbound,
                    Reference = orderNumber,
                    Reason = "Order placed",
                    CreatedAt = now
                });
            }

            if (coupon is not null)
            {
                coupon.UsedCount += 1;
            }

            order.Payments.Add(new Payment
            {
                Method = command.PaymentMethod,
                Provider = paymentProvider.Code,
                ProviderMessage = paymentProvider.RequiresManualVerification
                    ? "Awaiting manual payment verification"
                    : null,
                Amount = total,
                Currency = currency,
                Status = PaymentStatus.Pending,
                CreatedAt = now,
                UpdatedAt = now
            });

            order.StatusHistory.Add(new OrderStatusHistory
            {
                FromStatus = null,
                ToStatus = OrderStatus.Pending,
                Note = "Order created",
                CreatedAt = now
            });

            if (command.SaveAddress)
            {
                var addressCount = await _dbContext.Addresses
                    .CountAsync(a => a.UserId == userId, cancellationToken);

                if (addressCount < MaxAddressesPerCustomer)
                {
                    var hasDefault = await _dbContext.Addresses
                        .AnyAsync(a => a.UserId == userId && a.IsDefault, cancellationToken);

                    _dbContext.Addresses.Add(new Address
                    {
                        UserId = userId,
                        FullName = order.CustomerName,
                        PhoneNumber = order.CustomerPhone,
                        AddressLine1 = order.ShippingAddressLine1,
                        AddressLine2 = order.ShippingAddressLine2,
                        City = order.ShippingCity,
                        State = order.ShippingState,
                        PostalCode = order.ShippingPostalCode,
                        Country = order.ShippingCountry,
                        IsDefault = !hasDefault,
                        CreatedAt = now
                    });
                }
            }

            _dbContext.Orders.Add(order);
            _dbContext.CartItems.RemoveRange(cartItems);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Order {OrderNumber} placed by user {UserId} for {Total} {Currency}.",
                order.OrderNumber,
                userId,
                order.TotalAmount,
                currency);

            if (!string.IsNullOrWhiteSpace(order.CustomerEmail))
            {
                var email = EmailTemplates.OrderPlaced(
                    _configuration["Store:Name"] ?? "KStore",
                    order.CustomerName,
                    order.OrderNumber,
                    $"{order.TotalAmount.ToString("N2", CultureInfo.InvariantCulture)} {currency}",
                    paymentProvider.DisplayName);
                await SendOrderEmailSafelyAsync(order.CustomerEmail, email, cancellationToken);
            }

            return PlaceOrderResult.Ok(
                order.Id,
                order.OrderNumber,
                _t.Format("msg.orderPlaced", order.OrderNumber));
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogWarning(ex, "Concurrency conflict while placing an order for user {UserId}.", userId);

            return PlaceOrderResult.Failure(_t["msg.checkoutItemsChanged"]);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Order placement failed for user {UserId}.", userId);

            return PlaceOrderResult.Failure(_t["msg.orderPlaceFailed"]);
        }
    }

    public Task<PlaceOrderResult> CancelOrderAsync(
        int orderId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        return ExecuteCancelAsync(
            orderId,
            ownerUserId: userId,
            reason: _t["msg.cancelReasonCustomer"],
            actorUserId: userId,
            cancellationToken);
    }

    public Task<PlaceOrderResult> CancelOrderByStaffAsync(
        int orderId,
        string? staffUserId,
        string reason,
        CancellationToken cancellationToken = default)
    {
        return ExecuteCancelAsync(
            orderId,
            ownerUserId: null,
            reason: string.IsNullOrWhiteSpace(reason) ? _t["msg.cancelReasonStaff"] : reason.Trim(),
            actorUserId: staffUserId,
            cancellationToken);
    }

    public async Task<OrderStatusUpdateResult> UpdateStatusAsync(
        int orderId,
        OrderStatus newStatus,
        string? carrier,
        string? trackingNumber,
        string? note,
        string? staffUserId,
        CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order is null)
        {
            return OrderStatusUpdateResult.Fail(_t["msg.orderNotFound"]);
        }

        if (newStatus == OrderStatus.Cancelled)
        {
            return OrderStatusUpdateResult.Fail(_t["msg.useCancelAction"]);
        }

        if (!OrderStatusFlow.CanTransition(order.OrderStatus, newStatus))
        {
            return OrderStatusUpdateResult.Fail(
                _t.Format("msg.statusTransitionInvalid", _t["status." + order.OrderStatus], _t["status." + newStatus]));
        }

        var now = DateTime.UtcNow;
        var previousStatus = order.OrderStatus;

        order.OrderStatus = newStatus;
        order.UpdatedAt = now;

        switch (newStatus)
        {
            case OrderStatus.Confirmed:
                order.ConfirmedAt = now;
                break;
            case OrderStatus.Shipped:
                order.ShippedAt = now;
                break;
            case OrderStatus.Delivered:
                order.DeliveredAt = now;
                break;
        }

        if (!string.IsNullOrWhiteSpace(carrier))
        {
            order.ShippingCarrier = carrier.Trim();
        }

        if (!string.IsNullOrWhiteSpace(trackingNumber))
        {
            order.TrackingNumber = trackingNumber.Trim();
        }

        order.StatusHistory.Add(new OrderStatusHistory
        {
            FromStatus = previousStatus,
            ToStatus = newStatus,
            Note = string.IsNullOrWhiteSpace(note)
                ? _t.Format("msg.statusChangedByStaff", _t["status." + previousStatus], _t["status." + newStatus])
                : note.Trim(),
            ChangedByUserId = staffUserId,
            CreatedAt = now
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(order.CustomerEmail))
        {
            var email = EmailTemplates.OrderStatusChanged(
                _configuration["Store:Name"] ?? "KStore",
                order.CustomerName,
                order.OrderNumber,
                _t["status." + newStatus],
                order.ShippingCarrier,
                order.TrackingNumber);
            await SendOrderEmailSafelyAsync(order.CustomerEmail, email, cancellationToken);
        }

        _logger.LogInformation(
            "Order {OrderNumber} status changed from {From} to {To} by {UserId}.",
            order.OrderNumber,
            previousStatus,
            newStatus,
            staffUserId);

        return OrderStatusUpdateResult.Ok(
            _t.Format("msg.orderNowStatus", order.OrderNumber, _t["status." + newStatus]));
    }

    private async Task SendOrderEmailSafelyAsync(
        string toEmail,
        EmailContent email,
        CancellationToken cancellationToken)
    {
        try
        {
            await _emailSender.SendAsync(
                toEmail,
                email.Subject,
                email.HtmlBody,
                email.TextBody,
                cancellationToken);
        }
        catch (Exception ex)
        {
            // Email is a notification; a delivery failure must not undo a committed order.
            _logger.LogError(ex, "Could not send order notification email to {Email}.", toEmail);
        }
    }

    public async Task<OrderStatusUpdateResult> UpdateShippingAsync(
        int orderId,
        string? carrier,
        string? trackingNumber,
        string? staffUserId,
        CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order is null)
        {
            return OrderStatusUpdateResult.Fail(_t["msg.orderNotFound"]);
        }

        if (order.OrderStatus is OrderStatus.Delivered or OrderStatus.Cancelled)
        {
            return OrderStatusUpdateResult.Fail(_t["msg.shippingLocked"]);
        }

        var newCarrier = string.IsNullOrWhiteSpace(carrier) ? null : carrier.Trim();
        var newTracking = string.IsNullOrWhiteSpace(trackingNumber) ? null : trackingNumber.Trim();

        if (newCarrier is null && newTracking is null)
        {
            return OrderStatusUpdateResult.Fail(_t["msg.shippingRequired"]);
        }

        order.ShippingCarrier = newCarrier;
        order.TrackingNumber = newTracking;
        order.UpdatedAt = DateTime.UtcNow;

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Shipping details updated for order {OrderNumber} by {UserId}.",
            order.OrderNumber,
            staffUserId);

        return OrderStatusUpdateResult.Ok(_t.Format("msg.shippingUpdated", order.OrderNumber));
    }

    private async Task<PlaceOrderResult> ExecuteCancelAsync(
        int orderId,
        string? ownerUserId,
        string reason,
        string? actorUserId,
        CancellationToken cancellationToken)
    {
        var strategy = _dbContext.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            _dbContext.ChangeTracker.Clear();
            return await CancelInternalAsync(orderId, ownerUserId, reason, actorUserId, cancellationToken);
        });
    }

    private async Task<PlaceOrderResult> CancelInternalAsync(
        int orderId,
        string? ownerUserId,
        string reason,
        string? actorUserId,
        CancellationToken cancellationToken)
    {
        var pendingBakong = await _dbContext.Payments.AsNoTracking()
            .Where(p => p.OrderId == orderId
                        && p.Provider == BakongPaymentProvider.CodeValue
                        && p.Status == PaymentStatus.Pending
                        && p.QrMd5 != null)
            .Where(p => ownerUserId == null || p.Order!.UserId == ownerUserId)
            .Select(p => new { UserId = p.Order!.UserId, p.QrMd5 })
            .FirstOrDefaultAsync(cancellationToken);

        if (pendingBakong is not null)
        {
            var verification = await _paymentService.CheckBakongPaymentAsync(
                orderId,
                pendingBakong.UserId,
                cancellationToken);
            if (verification?.Status == PaymentStatus.Paid)
            {
                return PlaceOrderResult.Failure(
                    ownerUserId is null
                        ? _t["msg.paidOrderNoRefund"]
                        : _t["msg.paidOrderContactSupport"]);
            }

            if (verification?.CanCancel != true)
            {
                return PlaceOrderResult.Failure(_t["msg.bakongCancelUnverified"]);
            }
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var query = _dbContext.Orders
                .Include(o => o.Items)
                    .ThenInclude(i => i.ProductVariant)
                .Include(o => o.Coupon)
                .Include(o => o.Payments)
                .AsQueryable();

            if (ownerUserId is not null)
            {
                query = query.Where(o => o.UserId == ownerUserId);
            }

            var order = await query.FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

            if (order is null)
            {
                return PlaceOrderResult.Failure(_t["msg.orderNotFound"]);
            }

            if (order.PaymentStatus == PaymentStatus.Paid)
            {
                return PlaceOrderResult.Failure(
                    ownerUserId is null
                        ? _t["msg.paidOrderNoRefund"]
                        : _t["msg.paidOrderContactSupport"]);
            }

            var allowedStatuses = ownerUserId is null
                ? new[] { OrderStatus.Pending, OrderStatus.Confirmed, OrderStatus.Processing }
                : new[] { OrderStatus.Pending, OrderStatus.Confirmed };

            if (!allowedStatuses.Contains(order.OrderStatus))
            {
                return PlaceOrderResult.Failure(_t["msg.orderCancelTooLate"]);
            }

            var now = DateTime.UtcNow;
            var previousStatus = order.OrderStatus;

            foreach (var item in order.Items)
            {
                var variant = item.ProductVariant;

                if (variant is null)
                {
                    continue;
                }

                variant.StockQuantity += item.Quantity;

                _dbContext.StockMovements.Add(new StockMovement
                {
                    ProductVariant = variant,
                    QuantityChange = item.Quantity,
                    StockAfter = variant.StockQuantity,
                    Type = StockMovementType.Return,
                    Reference = order.OrderNumber,
                    Reason = reason,
                    PerformedByUserId = actorUserId,
                    CreatedAt = now
                });
            }

            order.OrderStatus = OrderStatus.Cancelled;
            order.CancelledAt = now;
            order.UpdatedAt = now;
            order.CancellationReason = reason;

            // Free the coupon usage so a cancelled order does not consume a limited coupon.
            if (order.Coupon is not null && order.Coupon.UsedCount > 0)
            {
                order.Coupon.UsedCount -= 1;
            }

            order.StatusHistory.Add(new OrderStatusHistory
            {
                FromStatus = previousStatus,
                ToStatus = OrderStatus.Cancelled,
                Note = reason,
                ChangedByUserId = actorUserId,
                CreatedAt = now
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            _logger.LogInformation(
                "Order {OrderNumber} cancelled. Reason: {Reason}. Actor: {UserId}.",
                order.OrderNumber,
                reason,
                actorUserId);

            return PlaceOrderResult.Ok(
                order.Id,
                order.OrderNumber,
                _t.Format("msg.orderCancelled", order.OrderNumber));
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogWarning(ex, "Concurrency conflict while cancelling order {OrderId}.", orderId);
            return PlaceOrderResult.Failure(_t["msg.orderChangedWhileCancelling"]);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogError(ex, "Cancelling order {OrderId} failed.", orderId);
            return PlaceOrderResult.Failure(_t["msg.orderCancelFailed"]);
        }
    }

    // ---------------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------------

    private string? CheckCoupon(Coupon coupon, decimal subtotal, DateTime now)
    {
        if (!coupon.IsActive)
        {
            return _t["msg.couponInactive"];
        }

        if (coupon.StartsAt > now)
        {
            return _t["msg.couponNotStarted"];
        }

        if (coupon.EndsAt.HasValue && coupon.EndsAt.Value < now)
        {
            return _t["msg.couponExpired"];
        }

        if (coupon.UsageLimit.HasValue && coupon.UsedCount >= coupon.UsageLimit.Value)
        {
            return _t["msg.couponUsageLimit"];
        }

        if (subtotal < coupon.MinimumOrderAmount)
        {
            return _t.Format("msg.couponMinOrder", FormatMoney(coupon.MinimumOrderAmount));
        }

        return null;
    }

    /// <summary>Formats an amount with the current culture's currency symbol.</summary>
    private static string FormatMoney(decimal amount) =>
        amount.ToString("C2", CultureInfo.CurrentCulture);

    private static decimal CalculateDiscount(Coupon coupon, decimal subtotal)
    {
        var discount = coupon.DiscountType == DiscountType.Percentage
            ? subtotal * coupon.DiscountValue / 100m
            : coupon.DiscountValue;

        if (coupon.MaxDiscountAmount.HasValue)
        {
            discount = Math.Min(discount, coupon.MaxDiscountAmount.Value);
        }

        discount = Math.Round(discount, 2, MidpointRounding.AwayFromZero);

        return Math.Clamp(discount, 0m, subtotal);
    }

    private async Task<string> GenerateOrderNumberAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 5; attempt++)
        {
            var candidate = $"RS-{DateTime.UtcNow:yyMMdd}-{RandomNumberGenerator.GetInt32(100000, 1000000)}";

            var exists = await _dbContext.Orders
                .AnyAsync(o => o.OrderNumber == candidate, cancellationToken);

            if (!exists)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Could not generate a unique order number.");
    }

    private static string? BuildVariantDescription(ProductVariant variant)
    {
        var parts = new[] { variant.Color, variant.Storage }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToList();

        return parts.Count == 0 ? null : string.Join(" / ", parts);
    }

    private static decimal ReadDecimal(
        IReadOnlyDictionary<string, string?> values,
        string key,
        decimal fallback)
    {
        return values.TryGetValue(key, out var raw)
               && decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    private static string? ReadString(IReadOnlyDictionary<string, string?> values, string key)
    {
        return values.TryGetValue(key, out var raw) && !string.IsNullOrWhiteSpace(raw)
            ? raw.Trim()
            : null;
    }
}
