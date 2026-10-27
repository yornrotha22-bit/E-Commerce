using System.Security.Claims;
using RothaShop.Data;
using RothaShop.Models;

namespace RothaShop.Services;

/// <summary>
/// Writes administrative audit entries. Never store passwords, tokens or payment secrets.
/// </summary>
public interface IAuditService
{
    Task LogAsync(
        string action,
        string entityName,
        string? entityId,
        string? details,
        CancellationToken cancellationToken = default);
}

public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuditService> _logger;

    public AuditService(
        ApplicationDbContext dbContext,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AuditService> logger)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task LogAsync(
        string action,
        string entityName,
        string? entityId,
        string? details,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var context = _httpContextAccessor.HttpContext;
            var userId = context?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            _dbContext.AuditLogs.Add(new AuditLog
            {
                UserId = string.IsNullOrWhiteSpace(userId) ? null : userId,
                Action = Trim(action, 100) ?? string.Empty,
                EntityName = Trim(entityName, 100) ?? string.Empty,
                EntityId = Trim(entityId, 50),
                Details = Trim(details, 2000),
                IpAddress = Trim(context?.Connection.RemoteIpAddress?.ToString(), 64),
                CreatedAt = DateTime.UtcNow
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Auditing must never break the main operation.
            _logger.LogWarning(ex, "Could not write audit log for {Action} on {Entity}.", action, entityName);
        }
    }

    private static string? Trim(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
