using DecisionSpark.Models.Runtime;

namespace DecisionSpark.Services;

/// <summary>
/// Persists conversation state to disk as JSON files
/// </summary>
public interface IConversationPersistence
{
    /// <summary>
    /// Saves a conversation snapshot to disk
    /// </summary>
    Task SaveConversationAsync(DecisionSession session);
}

/// <summary>
/// File-based conversation persistence
/// </summary>
public class FileConversationPersistence : IConversationPersistence
{
    private readonly string _conversationsPath;
    private readonly ILogger<FileConversationPersistence> _logger;

    public FileConversationPersistence(
        IConfiguration configuration,
        ILogger<FileConversationPersistence> logger)
    {
        _conversationsPath = configuration["ConversationStorage:Path"] ?? "conversations";
        _logger = logger;

        // Ensure directory exists
        try
        {
            Directory.CreateDirectory(_conversationsPath);
            _logger.LogInformation("Conversation storage path: {Path}", _conversationsPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create conversations directory: {Path}", _conversationsPath);
            throw;
        }
    }

    public async Task SaveConversationAsync(DecisionSession session)
    {
        try
        {
            var fileName = $"{session.SessionId}.json";
            var filePath = Path.Combine(_conversationsPath, fileName);

            var json = System.Text.Json.JsonSerializer.Serialize(session, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase
            });

            await File.WriteAllTextAsync(filePath, json);

            _logger.LogDebug("Saved conversation {SessionId} to {FilePath}", session.SessionId, filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save conversation {SessionId}", session.SessionId);
            // Don't throw - conversation persistence failure shouldn't break the API
        }
    }
}
