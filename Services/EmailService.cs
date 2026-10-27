using System.Net;
using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace RothaShop.Services;

/// <summary>
/// SMTP settings bound from the <c>Email</c> configuration section.
/// </summary>
public class EmailOptions
{
    public const string SectionName = "Email";

    public bool Enabled { get; set; }

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; } = 587;

    /// <summary>STARTTLS / implicit TLS depending on the server configuration.</summary>
    public bool UseStartTls { get; set; } = true;

    public string? UserName { get; set; }

    public string? Password { get; set; }

    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = "KStore";

    /// <summary>
    /// Email is sent only when it is explicitly enabled and the minimum settings
    /// are present. Otherwise the sender logs instead of throwing, so a missing
    /// SMTP server never breaks registration or password reset.
    /// </summary>
    public bool IsConfigured =>
        Enabled
        && !string.IsNullOrWhiteSpace(Host)
        && Port > 0
        && !string.IsNullOrWhiteSpace(FromAddress);
}

public interface IEmailSender
{
    Task SendAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string textBody,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Sends transactional email over SMTP. When <see cref="EmailOptions.IsConfigured"/>
/// is false the message is logged instead: in Development the full body (including
/// reset links) is written to the log, in other environments only a warning is
/// written so links never leak into production logs.
/// </summary>
public class EmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<EmailSender> _logger;

    public EmailSender(
        IOptions<EmailOptions> options,
        IWebHostEnvironment environment,
        ILogger<EmailSender> logger)
    {
        _options = options.Value;
        _environment = environment;
        _logger = logger;
    }

    public async Task SendAsync(
        string toEmail,
        string subject,
        string htmlBody,
        string textBody,
        CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
        {
            if (_environment.IsDevelopment())
            {
                _logger.LogInformation(
                    "Email delivery is disabled (Email:Enabled=false). Development copy:\nTo: {To}\nSubject: {Subject}\n{Body}",
                    toEmail,
                    subject,
                    textBody);
            }
            else
            {
                _logger.LogWarning(
                    "Email delivery is disabled (Email:Enabled=false). Message '{Subject}' to {To} was not sent.",
                    subject,
                    toEmail);
            }

            return;
        }

        using var message = new MailMessage
        {
            From = new MailAddress(_options.FromAddress, _options.FromName),
            Subject = subject,
            Body = htmlBody,
            IsBodyHtml = true,
            BodyEncoding = System.Text.Encoding.UTF8,
            SubjectEncoding = System.Text.Encoding.UTF8
        };

        message.To.Add(toEmail);

        using var client = new SmtpClient(_options.Host, _options.Port)
        {
            EnableSsl = _options.UseStartTls,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = 15000
        };

        if (!string.IsNullOrWhiteSpace(_options.UserName))
        {
            client.Credentials = new NetworkCredential(_options.UserName, _options.Password);
        }

        await client.SendMailAsync(message, cancellationToken);

        _logger.LogInformation("Email '{Subject}' sent to {To}.", subject, toEmail);
    }
}
