using System.Net;

namespace RothaShop.Services;

/// <summary>
/// Plain text and HTML bodies for transactional email. Values are HTML-encoded so
/// user-supplied names cannot inject markup into the message.
/// </summary>
public static class EmailTemplates
{
    public static EmailContent OrderPlaced(
        string storeName,
        string fullName,
        string orderNumber,
        string total,
        string paymentMethod)
    {
        var displayStore = string.IsNullOrWhiteSpace(storeName) ? "KStore" : storeName;
        var displayName = string.IsNullOrWhiteSpace(fullName) ? "there" : fullName;
        var safeStore = WebUtility.HtmlEncode(displayStore);
        var safeName = WebUtility.HtmlEncode(displayName);
        var safeOrder = WebUtility.HtmlEncode(orderNumber);
        var safeTotal = WebUtility.HtmlEncode(total);
        var safePayment = WebUtility.HtmlEncode(paymentMethod);
        var subject = $"{displayStore} order {orderNumber} received";
        var text = $"Hello {displayName},\n\nWe received your order {orderNumber}.\nTotal: {total}\nPayment: {paymentMethod}\n\nThank you for shopping with {displayStore}.";
        var html = $"<p>Hello {safeName},</p><p>We received your order <strong>{safeOrder}</strong>.</p><p>Total: {safeTotal}<br>Payment: {safePayment}</p><p>Thank you for shopping with {safeStore}.</p>";
        return new EmailContent(subject, html, text);
    }

    public static EmailContent OrderStatusChanged(
        string storeName,
        string fullName,
        string orderNumber,
        string status,
        string? carrier,
        string? trackingNumber)
    {
        var displayStore = string.IsNullOrWhiteSpace(storeName) ? "KStore" : storeName;
        var displayName = string.IsNullOrWhiteSpace(fullName) ? "there" : fullName;
        var details = new List<string>();
        if (!string.IsNullOrWhiteSpace(carrier)) details.Add($"Carrier: {carrier.Trim()}");
        if (!string.IsNullOrWhiteSpace(trackingNumber)) details.Add($"Tracking: {trackingNumber.Trim()}");
        var extraText = details.Count == 0 ? string.Empty : "\n" + string.Join("\n", details);
        var extraHtml = details.Count == 0
            ? string.Empty
            : "<p>" + string.Join("<br>", details.Select(value => WebUtility.HtmlEncode(value))) + "</p>";
        var subject = $"{displayStore} order {orderNumber}: {status}";
        var text = $"Hello {displayName},\n\nOrder {orderNumber} is now {status}.{extraText}\n\nThank you for shopping with {displayStore}.";
        var html = $"<p>Hello {WebUtility.HtmlEncode(displayName)},</p><p>Order <strong>{WebUtility.HtmlEncode(orderNumber)}</strong> is now {WebUtility.HtmlEncode(status)}.</p>{extraHtml}<p>Thank you for shopping with {WebUtility.HtmlEncode(displayStore)}.</p>";
        return new EmailContent(subject, html, text);
    }

    public static EmailContent PasswordReset(string storeName, string fullName, string resetLink)
    {
        var displayStore = string.IsNullOrWhiteSpace(storeName) ? "KStore" : storeName;
        var displayName = string.IsNullOrWhiteSpace(fullName) ? "there" : fullName;

        var safeStore = WebUtility.HtmlEncode(displayStore);
        var safeName = WebUtility.HtmlEncode(displayName);
        var safeLink = WebUtility.HtmlEncode(resetLink);

        var subject = $"{displayStore} password reset";

        var text = $"""
            Hello {displayName},

            We received a request to reset the password of your {displayStore} account.

            Open this link to choose a new password:
            {resetLink}

            If you did not request it, ignore this email; your current password stays unchanged.
            """;

        var html = $"""
            <p>Hello {safeName},</p>
            <p>We received a request to reset the password of your {safeStore} account.</p>
            <p><a href="{safeLink}">Choose a new password</a></p>
            <p>If you did not request it, ignore this email; your current password stays unchanged.</p>
            <p>— {safeStore}</p>
            """;

        return new EmailContent(subject, html, text);
    }
}

public record EmailContent(string Subject, string HtmlBody, string TextBody);
