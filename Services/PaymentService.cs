using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using RothaShop.Data;
using RothaShop.Localization;
using RothaShop.Models;
using kh.gov.nbc.bakong_khqr;
using kh.gov.nbc.bakong_khqr.model;

namespace RothaShop.Services;

/// <summary>
/// Public description of a payment method, used by checkout and admin screens.
/// </summary>
public class PaymentProviderInfo
{
    public string Code { get; init; } = string.Empty;

    public PaymentMethod Method { get; init; }

    public string DisplayName { get; init; } = string.Empty;

    public bool IsAvailable { get; init; }

    public string? UnavailableReason { get; init; }

    public bool RequiresManualVerification { get; init; }
}

/// <summary>
/// Instructions shown to a customer after an order is created with a manual
/// payment method. Nothing here is ever treated as proof of payment.
/// </summary>
public class PaymentInstructions
{
    public string ProviderCode { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public IReadOnlyList<string> Lines { get; init; } = Array.Empty<string>();

    public string? Reference { get; init; }

    public decimal Amount { get; init; }

    public string Currency { get; init; } = "USD";

    public string? QrPayload { get; init; }

    public string? QrMd5 { get; init; }

    public string? QrImageDataUrl { get; init; }

    public DateTime? ExpiresAt { get; init; }

    public bool IsBakong { get; init; }
}

public class PaymentStatusCheckResult
{
    public PaymentStatus Status { get; init; }

    public string Message { get; init; } = string.Empty;

    public bool CanCancel { get; init; }
}

public class PaymentUpdateResult
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    public static PaymentUpdateResult Ok(string message) => new() { Success = true, Message = message };

    public static PaymentUpdateResult Fail(string message) => new() { Success = false, Message = message };
}

/// <summary>
/// Contract for a payment method. A real gateway implementation (ABA PayWay,
/// Bakong, Stripe...) would implement this and perform server-side verification
/// in a signed webhook handler before any payment is marked as paid.
/// </summary>
public interface IPaymentProvider
{
    string Code { get; }

    PaymentMethod Method { get; }

    string DisplayName { get; }

    bool IsAvailable { get; }

    string? UnavailableReason { get; }

    bool RequiresManualVerification { get; }

    PaymentInstructions? GetInstructions(Order order);
}

public interface IPaymentService
{
    IReadOnlyList<PaymentProviderInfo> GetProviders();

    PaymentProviderInfo GetProvider(PaymentMethod method);

    bool IsMethodAvailable(PaymentMethod method);

    Task<PaymentInstructions?> GetInstructionsAsync(int orderId, CancellationToken cancellationToken = default);

    Task<PaymentStatusCheckResult?> CheckBakongPaymentAsync(
        int orderId,
        string userId,
        CancellationToken cancellationToken = default);

    Task<PaymentUpdateResult> MarkPaidAsync(
        int paymentId,
        string? staffUserId,
        CancellationToken cancellationToken = default);

    Task<PaymentUpdateResult> MarkFailedAsync(
        int paymentId,
        string? staffUserId,
        string? reason,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Cash on delivery: no external provider, cash is collected by the courier and
/// confirmed later by authorized staff.
/// </summary>
public class CodPaymentProvider : IPaymentProvider
{
    public const string CodeValue = "COD";

    private readonly ITranslator _t;

    public CodPaymentProvider(ITranslator translator)
    {
        _t = translator;
    }

    public string Code => CodeValue;

    public PaymentMethod Method => PaymentMethod.CashOnDelivery;

    public string DisplayName => _t["msg.paymentCodName"];

    public bool IsAvailable => true;

    public string? UnavailableReason => null;

    public bool RequiresManualVerification => false;

    public PaymentInstructions? GetInstructions(Order order) => null;
}

/// <summary>Creates an amount-bound dynamic KHQR for Bakong payment.</summary>
public class BakongPaymentProvider : IPaymentProvider
{
    public const string CodeValue = "BAKONG";

    private readonly IConfiguration _configuration;
    private readonly ITranslator _t;

    public BakongPaymentProvider(IConfiguration configuration, ITranslator translator)
    {
        _configuration = configuration;
        _t = translator;
    }

    public string Code => CodeValue;

    public PaymentMethod Method => PaymentMethod.OnlinePayment;

    public string DisplayName => _t["msg.paymentBakongName"];

    public bool IsAvailable =>
        _configuration.GetValue<bool>("Payments:Online:Enabled")
        && !string.IsNullOrWhiteSpace(AccountId)
        && AccountId!.Length <= 32
        && !string.IsNullOrWhiteSpace(MerchantName)
        && MerchantName!.Length <= 25
        && !string.IsNullOrWhiteSpace(ApiToken)
        && IsSupportedCurrency(_configuration["Store:Currency"] ?? "USD")
        && (string.IsNullOrWhiteSpace(MerchantCity) || MerchantCity.Length <= 15);

    public string? UnavailableReason
    {
        get
        {
            if (!_configuration.GetValue<bool>("Payments:Online:Enabled"))
            {
                return _t["msg.onlineNotEnabled"];
            }

            return IsAvailable
                ? null
                : _t["msg.bakongNotConfigured"];
        }
    }

    public bool RequiresManualVerification => false;

    private string? AccountId => _configuration["Payments:Bakong:AccountId"]?.Trim();

    private string? MerchantName => _configuration["Payments:Bakong:MerchantName"]?.Trim();

    private string? MerchantCity => _configuration["Payments:Bakong:MerchantCity"]?.Trim();

    private string? AcquiringBank => _configuration["Payments:Bakong:AcquiringBank"]?.Trim();

    private string? ApiToken => _configuration["Payments:Bakong:ApiToken"]?.Trim();

    private int ExpiryMinutes => Math.Clamp(_configuration.GetValue("Payments:Bakong:ExpiryMinutes", 5), 1, 10);

    private static bool IsSupportedCurrency(string currency) =>
        currency.Equals("USD", StringComparison.OrdinalIgnoreCase)
        || currency.Equals("KHR", StringComparison.OrdinalIgnoreCase);

    public PaymentInstructions? GetInstructions(Order order)
    {
        if (!IsAvailable)
        {
            return null;
        }

        var payment = order.Payments.FirstOrDefault(p => p.Provider == Code)
            ?? order.Payments.FirstOrDefault();
        if (payment is null)
        {
            return null;
        }

        var expiresAt = payment.QrExpiresAt;
        var qrPayload = payment.QrPayload;
        var md5 = payment.QrMd5;
        if (string.IsNullOrWhiteSpace(qrPayload) || string.IsNullOrWhiteSpace(md5)
            || expiresAt is null || expiresAt <= DateTime.UtcNow)
        {
            expiresAt = DateTime.UtcNow.AddMinutes(ExpiryMinutes);
            if (!IsSupportedCurrency(payment.Currency))
            {
                return null;
            }

            var currency = payment.Currency.Equals("KHR", StringComparison.OrdinalIgnoreCase)
                ? KHQRCurrency.KHR
                : KHQRCurrency.USD;
            var generated = BakongKHQR.GenerateIndividual(new IndividualInfo
            {
                BakongAccountID = AccountId!,
                Currency = currency,
                Amount = Convert.ToDouble(payment.Amount),
                MerchantName = MerchantName!,
                MerchantCity = string.IsNullOrWhiteSpace(MerchantCity) ? "Phnom Penh" : MerchantCity,
                AcquiringBank = AcquiringBank,
                BillNumber = order.OrderNumber,
                StoreLabel = _configuration["Store:Name"] ?? "KStore",
                ExpirationTimestamp = new DateTimeOffset(DateTime.SpecifyKind(expiresAt.Value, DateTimeKind.Utc)).ToUnixTimeMilliseconds()
            });

            if (generated.Status.Code != 0 || generated.Data is null)
            {
                return null;
            }

            qrPayload = generated.Data.QR;
            md5 = generated.Data.MD5;
        }

        var qrData = QRCodeGenerator.GenerateQrCode(qrPayload, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(qrData).GetGraphic(8);
        var imageDataUrl = "data:image/png;base64," + Convert.ToBase64String(png);
        var amountText = payment.Amount.ToString("N2", CultureInfo.InvariantCulture) + " " + payment.Currency;

        return new PaymentInstructions
        {
            ProviderCode = Code,
            Title = _t["msg.bakongInstructionsTitle"],
            Lines = new[]
            {
                _t.Format("msg.bakongAmount", amountText),
                _t.Format("msg.bankReference", order.OrderNumber),
                _t["msg.bakongScanInstructions"]
            },
            Reference = order.OrderNumber,
            Amount = payment.Amount,
            Currency = payment.Currency,
            QrPayload = qrPayload,
            QrMd5 = md5,
            QrImageDataUrl = imageDataUrl,
            ExpiresAt = expiresAt,
            IsBakong = true
        };
    }
}

public class PaymentService : IPaymentService
{
    private readonly IReadOnlyList<IPaymentProvider> _providers;
    private readonly ApplicationDbContext _dbContext;
    private readonly ITranslator _t;
    private readonly ILogger<PaymentService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public PaymentService(
        IEnumerable<IPaymentProvider> providers,
        ApplicationDbContext dbContext,
        ITranslator translator,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<PaymentService> logger)
    {
        _providers = providers.ToList();
        _dbContext = dbContext;
        _t = translator;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public IReadOnlyList<PaymentProviderInfo> GetProviders()
    {
        return _providers.Select(ToInfo).ToList();
    }

    public PaymentProviderInfo GetProvider(PaymentMethod method)
    {
        var provider = _providers.FirstOrDefault(p => p.Method == method);

        if (provider is null)
        {
            return new PaymentProviderInfo
            {
                Code = "UNKNOWN",
                Method = method,
                DisplayName = method.ToString(),
                IsAvailable = false,
                UnavailableReason = _t["msg.paymentMethodUnsupported"]
            };
        }

        return ToInfo(provider);
    }

    public bool IsMethodAvailable(PaymentMethod method) => GetProvider(method).IsAvailable;

    public async Task<PaymentInstructions?> GetInstructionsAsync(
        int orderId,
        CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .Include(o => o.Payments)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

        if (order is null)
        {
            return null;
        }

        // Instructions depend on the method chosen at checkout, not on whether
        // the method is currently enabled in configuration.
        var provider = _providers.FirstOrDefault(p => p.Method == order.PaymentMethod);
        var instructions = provider?.GetInstructions(order);
        var payment = order.Payments.FirstOrDefault(p => p.Provider == BakongPaymentProvider.CodeValue);
        if (payment is not null && instructions is { IsBakong: true })
        {
            payment.QrPayload = instructions.QrPayload;
            payment.QrMd5 = instructions.QrMd5;
            payment.QrExpiresAt = instructions.ExpiresAt;
            payment.UpdatedAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return instructions;
    }

    public async Task<PaymentStatusCheckResult?> CheckBakongPaymentAsync(
        int orderId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var order = await _dbContext.Orders
            .Include(o => o.Payments)
            .Include(o => o.StatusHistory)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.UserId == userId, cancellationToken);

        var payment = order?.Payments.FirstOrDefault(p => p.Provider == BakongPaymentProvider.CodeValue);
        if (order is null || payment is null)
        {
            return null;
        }

        if (payment.Status == PaymentStatus.Paid)
        {
            return new PaymentStatusCheckResult { Status = PaymentStatus.Paid, Message = _t["msg.bakongPaid"] };
        }

        if (payment.Status != PaymentStatus.Pending || order.OrderStatus == OrderStatus.Cancelled)
        {
            return new PaymentStatusCheckResult { Status = payment.Status, Message = _t["msg.bakongPending"] };
        }

        var token = _configuration["Payments:Bakong:ApiToken"]?.Trim();
        var accountId = _configuration["Payments:Bakong:AccountId"]?.Trim();
        var apiBaseUrl = (_configuration["Payments:Bakong:ApiBaseUrl"] ?? "https://api-bakong.nbc.gov.kh").TrimEnd('/');
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(accountId)
            || string.IsNullOrWhiteSpace(payment.QrMd5)
            || !Uri.TryCreate(apiBaseUrl, UriKind.Absolute, out var baseUri)
            || baseUri.Scheme != Uri.UriSchemeHttps
            || !(baseUri.Host.EndsWith(".nbc.gov.kh", StringComparison.OrdinalIgnoreCase)
                 || baseUri.Host.EndsWith(".nbc.org.kh", StringComparison.OrdinalIgnoreCase)))
        {
            return new PaymentStatusCheckResult { Status = PaymentStatus.Pending, Message = _t["msg.bakongPending"] };
        }

        try
        {
            var client = _httpClientFactory.CreateClient("Bakong");
            using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(baseUri, "/v1/check_transaction_by_md5"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = JsonContent.Create(new { md5 = payment.QrMd5 });
            using var response = await client.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Bakong check returned HTTP {StatusCode} for order {OrderNumber}.", (int)response.StatusCode, order.OrderNumber);
                return new PaymentStatusCheckResult { Status = PaymentStatus.Pending, Message = _t["msg.bakongPending"] };
            }

            var result = await response.Content.ReadFromJsonAsync<BakongCheckResponse>(cancellationToken: cancellationToken);
            if (result?.ResponseCode != 0 || result.Data is null)
            {
                var notPaid = result?.ResponseCode == 1 && result.ErrorCode is 1 or 3;
                var qrExpired = payment.QrExpiresAt is null
                    || payment.QrExpiresAt <= DateTime.UtcNow.AddSeconds(-30);
                return new PaymentStatusCheckResult
                {
                    Status = PaymentStatus.Pending,
                    Message = _t["msg.bakongPending"],
                    CanCancel = notPaid && qrExpired
                };
            }

            var paidAmount = ReadAmount(result.Data.Amount);
            var paidCurrency = result.Data.Currency?.Trim().ToUpperInvariant();
            if (paidAmount != payment.Amount
                || !string.Equals(paidCurrency, payment.Currency, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(result.Data.ToAccountId, accountId, StringComparison.OrdinalIgnoreCase)
                || string.IsNullOrWhiteSpace(result.Data.Hash))
            {
                _logger.LogWarning("Bakong transaction did not match expected amount, currency, or receiver for order {OrderNumber}.", order.OrderNumber);
                return new PaymentStatusCheckResult { Status = PaymentStatus.Pending, Message = _t["msg.bakongMismatch"] };
            }

            var transactionUsed = await _dbContext.Payments.AnyAsync(
                p => p.Id != payment.Id
                     && p.Provider == BakongPaymentProvider.CodeValue
                     && p.TransactionReference == result.Data.Hash,
                cancellationToken);
            if (transactionUsed)
            {
                return new PaymentStatusCheckResult { Status = PaymentStatus.Pending, Message = _t["msg.bakongMismatch"] };
            }

            var now = DateTime.UtcNow;
            payment.Status = PaymentStatus.Paid;
            payment.TransactionReference = result.Data.Hash;
            payment.PaidAt = now;
            payment.UpdatedAt = now;
            payment.ProviderMessage = "Verified by Bakong Open API";
            order.PaymentStatus = PaymentStatus.Paid;
            order.UpdatedAt = now;
            order.StatusHistory.Add(new OrderStatusHistory
            {
                FromStatus = order.OrderStatus,
                ToStatus = order.OrderStatus,
                Note = _t.Format("msg.paymentConfirmedNote", "Bakong"),
                CreatedAt = now
            });
            await _dbContext.SaveChangesAsync(cancellationToken);

            return new PaymentStatusCheckResult { Status = PaymentStatus.Paid, Message = _t["msg.bakongPaid"] };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Bakong status check failed for order {OrderNumber}.", order.OrderNumber);
            return new PaymentStatusCheckResult { Status = PaymentStatus.Pending, Message = _t["msg.bakongPending"] };
        }
    }

    private static decimal? ReadAmount(JsonElement amount)
    {
        if (amount.ValueKind == JsonValueKind.Number && amount.TryGetDecimal(out var number)) return number;
        if (amount.ValueKind == JsonValueKind.String && decimal.TryParse(amount.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out number)) return number;
        return null;
    }

    private sealed class BakongCheckResponse
    {
        [JsonPropertyName("responseCode")] public int? ResponseCode { get; init; }
        [JsonPropertyName("errorCode")] public int? ErrorCode { get; init; }
        [JsonPropertyName("data")] public BakongTransaction? Data { get; init; }
    }

    private sealed class BakongTransaction
    {
        [JsonPropertyName("hash")] public string? Hash { get; init; }
        [JsonPropertyName("toAccountId")] public string? ToAccountId { get; init; }
        [JsonPropertyName("currency")] public string? Currency { get; init; }
        [JsonPropertyName("amount")] public JsonElement Amount { get; init; }
    }

    /// <summary>
    /// Authorized staff confirmation (COD cash collected or bank transfer received).
    /// This is the ONLY place an administrator can mark a payment as paid.
    /// </summary>
    public async Task<PaymentUpdateResult> MarkPaidAsync(
        int paymentId,
        string? staffUserId,
        CancellationToken cancellationToken = default)
    {
        var payment = await _dbContext.Payments
            .Include(p => p.Order)
            .FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken);

        if (payment?.Order is null)
        {
            return PaymentUpdateResult.Fail(_t["msg.paymentNotFound"]);
        }

        if (payment.Status == PaymentStatus.Paid)
        {
            return PaymentUpdateResult.Fail(_t["msg.paymentAlreadyPaid"]);
        }

        if (payment.Status != PaymentStatus.Pending)
        {
            return PaymentUpdateResult.Fail(_t["msg.paymentOnlyPendingConfirm"]);
        }

        if (payment.Order.OrderStatus == OrderStatus.Cancelled)
        {
            return PaymentUpdateResult.Fail(_t["msg.paymentCancelledOrder"]);
        }

        if (payment.Amount != payment.Order.TotalAmount)
        {
            return PaymentUpdateResult.Fail(_t["msg.paymentAmountMismatch"]);
        }

        var now = DateTime.UtcNow;

        payment.Status = PaymentStatus.Paid;
        payment.PaidAt = now;
        payment.UpdatedAt = now;
        payment.FailureReason = null;

        payment.Order.PaymentStatus = PaymentStatus.Paid;
        payment.Order.UpdatedAt = now;

        payment.Order.StatusHistory.Add(new OrderStatusHistory
        {
            FromStatus = payment.Order.OrderStatus,
            ToStatus = payment.Order.OrderStatus,
            Note = _t.Format("msg.paymentConfirmedNote", payment.Provider),
            ChangedByUserId = staffUserId,
            CreatedAt = now
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Payment {PaymentId} for order {OrderNumber} marked paid by {UserId}.",
            payment.Id,
            payment.Order.OrderNumber,
            staffUserId);

        return PaymentUpdateResult.Ok(
            _t.Format("msg.paymentMarkedPaid", payment.Order.OrderNumber));
    }

    public async Task<PaymentUpdateResult> MarkFailedAsync(
        int paymentId,
        string? staffUserId,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var payment = await _dbContext.Payments
            .Include(p => p.Order)
            .FirstOrDefaultAsync(p => p.Id == paymentId, cancellationToken);

        if (payment?.Order is null)
        {
            return PaymentUpdateResult.Fail(_t["msg.paymentNotFound"]);
        }

        if (payment.Status == PaymentStatus.Paid)
        {
            return PaymentUpdateResult.Fail(_t["msg.paymentPaidCannotFail"]);
        }

        if (payment.Status != PaymentStatus.Pending)
        {
            return PaymentUpdateResult.Fail(_t["msg.paymentOnlyPendingFail"]);
        }

        var now = DateTime.UtcNow;

        payment.Status = PaymentStatus.Failed;
        payment.FailureReason = string.IsNullOrWhiteSpace(reason)
            ? _t["msg.paymentFailReasonStaff"]
            : reason.Trim();
        payment.UpdatedAt = now;

        payment.Order.PaymentStatus = PaymentStatus.Failed;
        payment.Order.UpdatedAt = now;

        payment.Order.StatusHistory.Add(new OrderStatusHistory
        {
            FromStatus = payment.Order.OrderStatus,
            ToStatus = payment.Order.OrderStatus,
            Note = _t.Format("msg.paymentFailedNote", payment.FailureReason),
            ChangedByUserId = staffUserId,
            CreatedAt = now
        });

        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Payment {PaymentId} for order {OrderNumber} marked failed by {UserId}.",
            payment.Id,
            payment.Order.OrderNumber,
            staffUserId);

        return PaymentUpdateResult.Ok(
            _t.Format("msg.paymentMarkedFailed", payment.Order.OrderNumber));
    }

    private static PaymentProviderInfo ToInfo(IPaymentProvider provider) => new()
    {
        Code = provider.Code,
        Method = provider.Method,
        DisplayName = provider.DisplayName,
        IsAvailable = provider.IsAvailable,
        UnavailableReason = provider.UnavailableReason,
        RequiresManualVerification = provider.RequiresManualVerification
    };
}
