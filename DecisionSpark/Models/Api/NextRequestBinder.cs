using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace DecisionSpark.Models.Api;

public class NextRequestBinder : IModelBinder
{
    public async Task BindModelAsync(ModelBindingContext bindingContext)
    {
        if (bindingContext == null)
        {
            throw new ArgumentNullException(nameof(bindingContext));
        }

        // Get logger from services
        var logger = bindingContext.HttpContext.RequestServices
            .GetService<ILogger<NextRequestBinder>>();

        var request = bindingContext.HttpContext.Request;
        
        logger?.LogInformation("[NextRequestBinder] Starting model binding");
        
        // Enable buffering so the body can be read multiple times
        request.EnableBuffering();
        
        if (request.Body == null)
        {
            logger?.LogWarning("[NextRequestBinder] Request body is null");
            bindingContext.Result = ModelBindingResult.Success(new NextRequest());
            return;
        }

        try
        {
            // Reset stream position to beginning
            request.Body.Position = 0;
            
            using var reader = new StreamReader(
                request.Body,
                encoding: Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 1024,
                leaveOpen: true);  // Leave stream open
            
            var body = await reader.ReadToEndAsync();
            
            logger?.LogInformation("[NextRequestBinder] Raw body length: {Length}", body.Length);
            logger?.LogInformation("[NextRequestBinder] Raw body: {Body}", body);

            if (string.IsNullOrWhiteSpace(body))
            {
                logger?.LogWarning("[NextRequestBinder] Body is empty after reading");
                bindingContext.Result = ModelBindingResult.Success(new NextRequest());
                return;
            }

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
                // Let JsonPropertyName attributes handle the mapping
            };

            var nextRequest = JsonSerializer.Deserialize<NextRequest>(body, options);
            
            logger?.LogInformation("[NextRequestBinder] Deserialized UserInput: '{UserInput}' (Length: {Length})", 
                nextRequest?.UserInput ?? "NULL", 
                nextRequest?.UserInput?.Length ?? 0);

            if (nextRequest == null)
            {
                logger?.LogWarning("[NextRequestBinder] Deserialization returned null");
                bindingContext.Result = ModelBindingResult.Success(new NextRequest());
                return;
            }

            // Validate and log selected_option_ids
            if (nextRequest.SelectedOptionIds != null && nextRequest.SelectedOptionIds.Length > 0)
            {
                // Enforce max 7 options limit per FR-024a
                if (nextRequest.SelectedOptionIds.Length > 7)
                {
                    logger?.LogWarning("[NextRequestBinder] Too many options selected: {Count}. Limiting to first 7.",
                        nextRequest.SelectedOptionIds.Length);
                    nextRequest.SelectedOptionIds = nextRequest.SelectedOptionIds.Take(7).ToArray();
                }

                logger?.LogInformation("[NextRequestBinder] Selected option IDs: [{Ids}]",
                    string.Join(", ", nextRequest.SelectedOptionIds));

                // Log when custom text is ignored per FR-024a
                if (!string.IsNullOrWhiteSpace(nextRequest.UserInput))
                {
                    logger?.LogInformation("[NextRequestBinder] Custom text ignored when structured selections present: '{UserInput}'",
                        nextRequest.UserInput);
                }
            }

            // Reset stream position for any subsequent reads
            request.Body.Position = 0;

            logger?.LogInformation("[NextRequestBinder] Model binding successful");
            bindingContext.Result = ModelBindingResult.Success(nextRequest);
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "[NextRequestBinder] Error during model binding");
            // Return empty request instead of failing
            bindingContext.Result = ModelBindingResult.Success(new NextRequest());
        }
    }
}
