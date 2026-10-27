using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace RothaShop.Services;

/// <summary>
/// Stores product images safely: validated type/size, generated file name,
/// and a fixed upload root that client input can never escape.
/// </summary>
public interface IImageService
{
    /// <summary>
    /// Validates and stores an uploaded image. Returns the public relative URL,
    /// or null when the file is missing/invalid (reason is logged and exposed via <see cref="LastError"/>).
    /// </summary>
    Task<string?> SaveProductImageAsync(IFormFile? file, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates and stores an uploaded profile photo. Returns the public
    /// relative URL, or null when the file is missing/invalid.
    /// </summary>
    Task<string?> SaveAvatarImageAsync(IFormFile? file, CancellationToken cancellationToken = default);

    /// <summary>Stores a validated brand logo under its own public upload folder.</summary>
    Task<string?> SaveBrandImageAsync(IFormFile? file, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a previously stored product image. Only paths inside the product upload
    /// folder are accepted.
    /// </summary>
    bool TryDelete(string? relativeUrl);

    /// <summary>
    /// Deletes a previously stored profile photo. Only paths inside the avatar
    /// upload folder are accepted.
    /// </summary>
    bool TryDeleteAvatar(string? relativeUrl);

    /// <summary>Deletes a previously stored brand logo.</summary>
    bool TryDeleteBrandImage(string? relativeUrl);

    /// <summary>
    /// Human readable reason of the last failed save attempt.
    /// </summary>
    string? LastError { get; }
}

public class ImageService : IImageService
{
    public const long MaxFileSizeBytes = 5 * 1024 * 1024;
    public const int MaxGalleryImages = 8;

    private const string UploadFolder = "uploads/products";
    private const string PublicPrefix = "/uploads/products/";

    private const string AvatarUploadFolder = "uploads/avatars";
    private const string AvatarPublicPrefix = "/uploads/avatars/";

    private const string BrandUploadFolder = "uploads/brands";
    private const string BrandPublicPrefix = "/uploads/brands/";

    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

    private static readonly string[] AllowedContentTypes =
    {
        "image/jpeg",
        "image/png",
        "image/webp"
    };

    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<ImageService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public ImageService(
        IWebHostEnvironment environment,
        ILogger<ImageService> logger,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _environment = environment;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public string? LastError { get; private set; }

    public Task<string?> SaveProductImageAsync(
        IFormFile? file,
        CancellationToken cancellationToken = default)
        => SaveImageAsync(file, UploadFolder, PublicPrefix, cancellationToken);

    public Task<string?> SaveAvatarImageAsync(
        IFormFile? file,
        CancellationToken cancellationToken = default)
        => SaveImageAsync(file, AvatarUploadFolder, AvatarPublicPrefix, cancellationToken);

    public Task<string?> SaveBrandImageAsync(
        IFormFile? file,
        CancellationToken cancellationToken = default)
        => SaveImageAsync(file, BrandUploadFolder, BrandPublicPrefix, cancellationToken);

    private async Task<string?> SaveImageAsync(
        IFormFile? file,
        string uploadFolder,
        string publicPrefix,
        CancellationToken cancellationToken)
    {
        LastError = null;

        if (file is null || file.Length == 0)
        {
            LastError = "No file was uploaded.";
            return null;
        }

        if (file.Length > MaxFileSizeBytes)
        {
            LastError = "The image is larger than 5 MB.";
            return null;
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

        if (!AllowedExtensions.Contains(extension))
        {
            LastError = "Only JPG, PNG and WEBP images are allowed.";
            return null;
        }

        if (!AllowedContentTypes.Contains(file.ContentType?.ToLowerInvariant()))
        {
            LastError = "The uploaded file is not a supported image type.";
            return null;
        }

        if (!await HasValidSignatureAsync(file, extension, cancellationToken))
        {
            LastError = "The uploaded file is not a valid image.";
            return null;
        }

        if (CloudinaryIsConfigured())
        {
            return await UploadToCloudinaryAsync(file, uploadFolder, extension, cancellationToken);
        }

        var now = DateTime.UtcNow;
        var relativeFolder = Path.Combine(uploadFolder, now.ToString("yyyy"), now.ToString("MM"));
        var absoluteFolder = Path.Combine(_environment.WebRootPath, relativeFolder);
        Directory.CreateDirectory(absoluteFolder);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var absolutePath = Path.Combine(absoluteFolder, fileName);

        await using (var target = new FileStream(
            absolutePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true))
        {
            await file.CopyToAsync(target, cancellationToken);
        }

        return $"{publicPrefix}{now:yyyy}/{now:MM}/{fileName}";
    }

    public bool TryDelete(string? relativeUrl)
        => TryDeleteIn(relativeUrl, UploadFolder, PublicPrefix);

    public bool TryDeleteAvatar(string? relativeUrl)
        => TryDeleteIn(relativeUrl, AvatarUploadFolder, AvatarPublicPrefix);

    public bool TryDeleteBrandImage(string? relativeUrl)
        => TryDeleteIn(relativeUrl, BrandUploadFolder, BrandPublicPrefix);

    private bool TryDeleteIn(string? relativeUrl, string uploadFolder, string publicPrefix)
    {
        if (IsCloudinaryUrl(relativeUrl))
        {
            return TryDeleteFromCloudinary(relativeUrl!, uploadFolder);
        }

        if (string.IsNullOrWhiteSpace(relativeUrl)
            || !relativeUrl.StartsWith(publicPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var uploadRoot = Path.GetFullPath(Path.Combine(_environment.WebRootPath, uploadFolder));
        var candidate = Path.GetFullPath(
            Path.Combine(_environment.WebRootPath, relativeUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar)));

        // Defense in depth: never delete outside the product upload folder.
        if (!candidate.StartsWith(uploadRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || !File.Exists(candidate))
        {
            return false;
        }

        try
        {
            File.Delete(candidate);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete image {Path}.", relativeUrl);
            return false;
        }
    }

    private bool CloudinaryIsConfigured()
        => !string.IsNullOrWhiteSpace(_configuration["Cloudinary:CloudName"])
           && !string.IsNullOrWhiteSpace(_configuration["Cloudinary:ApiKey"])
           && !string.IsNullOrWhiteSpace(_configuration["Cloudinary:ApiSecret"]);

    private async Task<string?> UploadToCloudinaryAsync(
        IFormFile file,
        string uploadFolder,
        string extension,
        CancellationToken cancellationToken)
    {
        var cloudName = _configuration["Cloudinary:CloudName"]!;
        var apiKey = _configuration["Cloudinary:ApiKey"]!;
        var apiSecret = _configuration["Cloudinary:ApiSecret"]!;
        var category = uploadFolder switch
        {
            UploadFolder => "products",
            AvatarUploadFolder => "avatars",
            BrandUploadFolder => "brands",
            _ => throw new InvalidOperationException("Unsupported image category.")
        };
        var date = DateTime.UtcNow;
        var publicId = $"rothashop/{category}/{date:yyyy}/{date:MM}/{Guid.NewGuid():N}";
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var signature = CreateCloudinarySignature(
            new Dictionary<string, string> { ["public_id"] = publicId, ["timestamp"] = timestamp }, apiSecret);

        await using var input = file.OpenReadStream();
        using var content = new MultipartFormDataContent();
        using var imageContent = new StreamContent(input);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
        content.Add(imageContent, "file", $"image{extension}");
        content.Add(new StringContent(apiKey), "api_key");
        content.Add(new StringContent(timestamp), "timestamp");
        content.Add(new StringContent(publicId), "public_id");
        content.Add(new StringContent(signature), "signature");

        try
        {
            using var response = await _httpClientFactory.CreateClient().PostAsync(
                $"https://api.cloudinary.com/v1_1/{Uri.EscapeDataString(cloudName)}/image/upload",
                content,
                cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Cloudinary image upload failed with status {StatusCode}: {Body}", response.StatusCode, body);
                LastError = "The image could not be uploaded to cloud storage.";
                return null;
            }

            using var json = JsonDocument.Parse(body);
            if (json.RootElement.TryGetProperty("secure_url", out var secureUrl)
                && Uri.TryCreate(secureUrl.GetString(), UriKind.Absolute, out var uri)
                && uri.Scheme == Uri.UriSchemeHttps)
            {
                return uri.ToString();
            }

            _logger.LogError("Cloudinary upload response did not contain a valid secure URL.");
            LastError = "Cloud storage returned an invalid image URL.";
            return null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogError(ex, "Cloudinary image upload failed.");
            LastError = "The image could not be uploaded to cloud storage.";
            return null;
        }
    }

    private bool TryDeleteFromCloudinary(string url, string uploadFolder)
    {
        if (!TryGetCloudinaryPublicId(url, uploadFolder, out var publicId))
        {
            return false;
        }

        var cloudName = _configuration["Cloudinary:CloudName"];
        var apiKey = _configuration["Cloudinary:ApiKey"];
        var apiSecret = _configuration["Cloudinary:ApiSecret"];
        if (string.IsNullOrWhiteSpace(cloudName) || string.IsNullOrWhiteSpace(apiKey)
            || string.IsNullOrWhiteSpace(apiSecret))
        {
            return false;
        }

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var signature = CreateCloudinarySignature(
            new Dictionary<string, string> { ["public_id"] = publicId, ["timestamp"] = timestamp }, apiSecret);
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["public_id"] = publicId,
            ["api_key"] = apiKey,
            ["timestamp"] = timestamp,
            ["signature"] = signature
        });

        try
        {
            using var response = _httpClientFactory.CreateClient().PostAsync(
                    $"https://api.cloudinary.com/v1_1/{Uri.EscapeDataString(cloudName)}/image/destroy",
                    content)
                .GetAwaiter().GetResult();
            var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Cloudinary image deletion failed with status {StatusCode}.", response.StatusCode);
                return false;
            }

            using var json = JsonDocument.Parse(body);
            return json.RootElement.TryGetProperty("result", out var result)
                   && result.GetString() is "ok" or "not found";
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "Cloudinary image deletion failed.");
            return false;
        }
    }

    private static bool IsCloudinaryUrl(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri)
           && uri.Scheme == Uri.UriSchemeHttps
           && uri.Host.Equals("res.cloudinary.com", StringComparison.OrdinalIgnoreCase);

    private static bool TryGetCloudinaryPublicId(string url, string uploadFolder, out string publicId)
    {
        publicId = string.Empty;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var uploadIndex = Array.FindIndex(parts, part => part.Equals("upload", StringComparison.Ordinal));
        if (uploadIndex < 0 || uploadIndex + 1 >= parts.Length) return false;

        var assetParts = parts.Skip(uploadIndex + 1).ToArray();
        if (assetParts.Length > 0 && System.Text.RegularExpressions.Regex.IsMatch(assetParts[0], "^v[0-9]+$"))
            assetParts = assetParts.Skip(1).ToArray();
        if (assetParts.Length == 0) return false;

        var last = assetParts[^1];
        var extensionIndex = last.LastIndexOf('.');
        if (extensionIndex > 0) assetParts[^1] = last[..extensionIndex];
        publicId = string.Join('/', assetParts);
        var category = uploadFolder switch
        {
            UploadFolder => "products",
            AvatarUploadFolder => "avatars",
            BrandUploadFolder => "brands",
            _ => string.Empty
        };
        return category.Length > 0
               && publicId.StartsWith($"rothashop/{category}/", StringComparison.Ordinal)
               && !publicId.Contains("..", StringComparison.Ordinal);
    }

    private static string CreateCloudinarySignature(IReadOnlyDictionary<string, string> parameters, string apiSecret)
    {
        var value = string.Join('&', parameters.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"{pair.Key}={pair.Value}")) + apiSecret;
        return Convert.ToHexStringLower(SHA1.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private static async Task<bool> HasValidSignatureAsync(
        IFormFile file,
        string extension,
        CancellationToken cancellationToken)
    {
        var header = new byte[12];

        await using var stream = file.OpenReadStream();
        var read = await stream.ReadAsync(header.AsMemory(0, header.Length), cancellationToken);

        if (read < 12)
        {
            return false;
        }

        return extension switch
        {
            ".jpg" or ".jpeg" => header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            ".png" => header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47,
            // RIFF????WEBP
            ".webp" => header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46
                       && header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50,
            _ => false
        };
    }
}
