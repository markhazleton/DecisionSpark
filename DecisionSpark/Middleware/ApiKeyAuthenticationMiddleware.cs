using DecisionSpark.Core.Common;

namespace DecisionSpark.Middleware;

/// <summary>
/// Middleware for API key authentication
/// Validates the X-API-KEY header for protected endpoints
/// </summary>
public class ApiKeyAuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyAuthenticationMiddleware> _logger;
    private readonly string? _validApiKey;

    private static readonly HashSet<string> _exemptPaths = new(StringComparer.OrdinalIgnoreCase)
    {
        "/swagger",
        "/health",
        "/about",
        "/"
    };

    public ApiKeyAuthenticationMiddleware(
        RequestDelegate next,
        ILogger<ApiKeyAuthenticationMiddleware> logger,
        IConfiguration configuration)
    {
        _next = next;
        _logger = logger;
        _validApiKey = configuration[Constants.API_KEY_CONFIG];
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Skip authentication for exempt paths (swagger, home pages)
        if (IsExemptPath(context.Request.Path))
        {
            await _next(context);
            return;
        }

        // Check for API key in header
        if (!context.Request.Headers.TryGetValue(Constants.API_KEY_HEADER, out var apiKey))
        {
            _logger.LogWarning("Missing API key header for path: {Path}", context.Request.Path);
            await WriteUnauthorizedResponse(context, "Missing API key");
            return;
        }

        // Validate API key
        if (string.IsNullOrEmpty(apiKey) || apiKey != _validApiKey)
        {
            _logger.LogWarning("Invalid API key attempted for path: {Path}", context.Request.Path);
            await WriteUnauthorizedResponse(context, "Invalid API key");
            return;
        }

        // API key is valid, continue to next middleware
        await _next(context);
    }

    private static bool IsExemptPath(PathString path)
    {
        // Exact match for exempt paths
        if (_exemptPaths.Contains(path.Value ?? string.Empty))
        {
            return true;
        }

        // Check if path starts with any exempt path
        foreach (var exemptPath in _exemptPaths)
        {
            if (path.StartsWithSegments(exemptPath, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        // Check for static files (js, css, images)
        var extension = Path.GetExtension(path.Value ?? string.Empty);
        if (!string.IsNullOrEmpty(extension))
        {
            var staticExtensions = new[] { ".js", ".css", ".png", ".jpg", ".jpeg", ".gif", ".svg", ".ico", ".woff", ".woff2", ".ttf" };
            if (staticExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task WriteUnauthorizedResponse(HttpContext context, string message)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new
        {
            error = message,
            code = Constants.ErrorCodes.INVALID_API_KEY
        });
    }
}

/// <summary>
/// Extension methods for adding API key authentication middleware
/// </summary>
public static class ApiKeyAuthenticationMiddlewareExtensions
{
    /// <summary>
    /// Adds API key authentication middleware to the application pipeline
    /// </summary>
    public static IApplicationBuilder UseApiKeyAuthentication(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ApiKeyAuthenticationMiddleware>();
    }
}
