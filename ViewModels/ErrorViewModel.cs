namespace RothaShop.ViewModels;

public class ErrorViewModel
{
    public int StatusCode { get; set; } = 500;

    public string? RequestId { get; set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);

    public string Title => StatusCode switch
    {
        400 => "Bad request",
        401 => "Sign in required",
        403 => "Access denied",
        404 => "Page not found",
        500 => "Something went wrong",
        _ => "Unexpected error"
    };

    public string Message => StatusCode switch
    {
        400 => "The request could not be processed because some information was invalid.",
        401 => "You need to sign in before you can open this page.",
        403 => "You do not have permission to view this page.",
        404 => "The page you are looking for does not exist or has been moved.",
        500 => "An unexpected error occurred. Please try again or contact customer support.",
        _ => "The request could not be completed."
    };
}
