using Azure;
using Azure.AI.OpenAI;
using OpenAI.Chat;

namespace DecisionSpark.Core.Services;

/// <summary>
/// Implementation of OpenAI service supporting both OpenAI and Azure OpenAI
/// </summary>
public class OpenAIService : IOpenAIService
{
    private readonly ILogger<OpenAIService> _logger;
    private readonly OpenAIConfiguration _config;
    private readonly object? _client; // Can be AzureOpenAIClient or OpenAIClient

    public OpenAIService(ILogger<OpenAIService> logger, IConfiguration configuration)
    {
        _logger = logger;
        _config = configuration.GetSection("OpenAI").Get<OpenAIConfiguration>() ?? new OpenAIConfiguration();

        // Initialize client if configuration is valid
        if (IsConfigurationValid())
        {
            try
            {
                if (_config.Provider?.ToUpper() == "AZURE")
                {
                    // Azure OpenAI client
                    _client = new AzureOpenAIClient(
                        new Uri(_config.Endpoint!),
                        new AzureKeyCredential(_config.ApiKey));
                    _logger.LogInformation("Azure OpenAI client initialized successfully");
                }
                else
                {
                    // Direct OpenAI API client
                    _client = new global::OpenAI.OpenAIClient(_config.ApiKey);
                    _logger.LogInformation("OpenAI API client initialized successfully for model: {Model}", 
                        _config.Model ?? "gpt-4");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize OpenAI client");
            }
        }
        else
        {
            _logger.LogWarning("OpenAI configuration is incomplete. Service will use fallback mode.");
        }
    }

    public bool IsAvailable()
    {
        return _client != null && IsConfigurationValid();
    }

    public async Task<OpenAICompletionResponse> GetCompletionAsync(OpenAICompletionRequest request)
    {
        // If OpenAI is not available and fallback is enabled, return empty response
        if (!IsAvailable())
        {
            _logger.LogWarning("OpenAI not available, using fallback");
            return new OpenAICompletionResponse
            {
                Success = _config.EnableFallback,
                Content = string.Empty,
                ErrorMessage = "OpenAI not configured",
                UsedFallback = true
            };
        }

        try
        {
            ChatClient chatClient;
            
            if (_config.Provider?.ToUpper() == "AZURE")
            {
                // Azure OpenAI uses deployment name
                var azureClient = (AzureOpenAIClient)_client!;
                chatClient = azureClient.GetChatClient(_config.DeploymentName!);
            }
            else
            {
                // Direct OpenAI uses model name
                var openAIClient = (global::OpenAI.OpenAIClient)_client!;
                chatClient = openAIClient.GetChatClient(_config.Model ?? "gpt-4");
            }

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(request.SystemPrompt),
                new UserChatMessage(request.UserPrompt)
            };

            var options = new ChatCompletionOptions
            {
                MaxOutputTokenCount = request.MaxTokens ?? _config.MaxTokens,
                Temperature = request.Temperature ?? _config.Temperature
            };

            var modelInfo = _config.Provider?.ToUpper() == "AZURE" 
                ? $"Azure deployment: {_config.DeploymentName}" 
                : $"OpenAI model: {_config.Model ?? "gpt-4"}";
                
            _logger.LogInformation("Requesting OpenAI completion. {ModelInfo}, MaxTokens: {MaxTokens}",
                modelInfo, options.MaxOutputTokenCount);

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_config.TimeoutSeconds));
            var response = await chatClient.CompleteChatAsync(messages, options, cts.Token);

            var content = response.Value.Content[0].Text;

            _logger.LogInformation("OpenAI completion received. Length: {Length}", content.Length);

            return new OpenAICompletionResponse
            {
                Success = true,
                Content = content,
                UsedFallback = false
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogError("OpenAI request timed out after {Timeout} seconds", _config.TimeoutSeconds);
            return new OpenAICompletionResponse
            {
                Success = false,
                ErrorMessage = "Request timed out",
                UsedFallback = _config.EnableFallback
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling OpenAI");
            return new OpenAICompletionResponse
            {
                Success = false,
                ErrorMessage = ex.Message,
                UsedFallback = _config.EnableFallback
            };
        }
    }

    private bool IsConfigurationValid()
    {
        // Check if API key is valid (not empty or placeholder)
        if (string.IsNullOrWhiteSpace(_config.ApiKey) || 
            _config.ApiKey.Contains("your-") ||
            _config.ApiKey.Contains("mock-"))
        {
            return false;
        }

        var provider = _config.Provider?.ToUpper() ?? "OPENAI";

        if (provider == "AZURE")
        {
            // Azure requires Endpoint and DeploymentName
            return !string.IsNullOrWhiteSpace(_config.Endpoint) &&
                   !string.IsNullOrWhiteSpace(_config.DeploymentName) &&
                   !_config.Endpoint.Contains("YOUR-") &&
                   !_config.Endpoint.Contains("mock");
        }
        else
        {
            // Direct OpenAI just needs a valid API key and model
            // Model is optional (defaults to gpt-4)
            return true;
        }
    }
}
