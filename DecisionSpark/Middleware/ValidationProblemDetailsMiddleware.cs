using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace DecisionSpark.Middleware;

/// <summary>
/// Middleware to standardize validation error responses per FR-011.
/// Catches validation exceptions and converts them to standard ProblemDetails format.
/// </summary>
public class ValidationProblemDetailsMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ValidationProblemDetailsMiddleware> _logger;

    public ValidationProblemDetailsMiddleware(
        RequestDelegate next,
        ILogger<ValidationProblemDetailsMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (FluentValidation.ValidationException validationEx)
        {
            _logger.LogWarning(validationEx, "Validation error occurred");
            
            var errors = validationEx.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(e => e.ErrorMessage).ToArray()
                );

            var problemDetails = new ValidationProblemDetails(errors)
            {
                Title = "One or more validation errors occurred.",
                Status = StatusCodes.Status400BadRequest,
                Detail = validationEx.Message
            };

            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/problem+json";

            await context.Response.WriteAsync(JsonSerializer.Serialize(problemDetails, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred");
            
            var problemDetails = new ProblemDetails
            {
                Title = "An error occurred while processing your request.",
                Status = StatusCodes.Status500InternalServerError,
                Detail = context.RequestServices.GetRequiredService<IWebHostEnvironment>().IsDevelopment() 
                    ? ex.ToString() 
                    : "An internal server error occurred."
            };

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";

            await context.Response.WriteAsync(JsonSerializer.Serialize(problemDetails, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }));
        }
    }
}

/// <summary>
/// Extension methods for registering ValidationProblemDetails middleware.
/// </summary>
public static class ValidationProblemDetailsMiddlewareExtensions
{
    public static IApplicationBuilder UseValidationProblemDetails(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ValidationProblemDetailsMiddleware>();
    }
}
