namespace DecisionSpark.Core.Services;

/// <summary>
/// Configuration for OpenAI service
/// </summary>
public class OpenAIConfiguration
{
    /// <summary>
    /// Provider: "OpenAI" for direct OpenAI API, "Azure" for Azure OpenAI
    /// </summary>
    public string Provider { get; set; } = "OpenAI";
    
    /// <summary>
    /// Azure OpenAI endpoint (only used when Provider = "Azure")
    /// </summary>
    public string? Endpoint { get; set; }
    
    /// <summary>
    /// API Key (required for both providers)
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;
    
    /// <summary>
    /// Model name for OpenAI (e.g., "gpt-4", "gpt-3.5-turbo")
    /// </summary>
    public string? Model { get; set; }
    
    /// <summary>
    /// Azure deployment name (only used when Provider = "Azure")
    /// </summary>
    public string? DeploymentName { get; set; }
    
    public int MaxTokens { get; set; } = 500;
    public float Temperature { get; set; } = 0.7f;
    public bool EnableFallback { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 30;
}

/// <summary>
/// Request for OpenAI completion
/// </summary>
public class OpenAICompletionRequest
{
    public string SystemPrompt { get; set; } = string.Empty;
    public string UserPrompt { get; set; } = string.Empty;
    public int? MaxTokens { get; set; }
    public float? Temperature { get; set; }
}

/// <summary>
/// Response from OpenAI completion
/// </summary>
public class OpenAICompletionResponse
{
    public bool Success { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public bool UsedFallback { get; set; }
}

/// <summary>
/// Service for interacting with Azure OpenAI
/// </summary>
public interface IOpenAIService
{
    /// <summary>
    /// Get a completion from OpenAI
    /// </summary>
    Task<OpenAICompletionResponse> GetCompletionAsync(OpenAICompletionRequest request);

    /// <summary>
    /// Check if OpenAI is configured and available
    /// </summary>
    bool IsAvailable();
}
