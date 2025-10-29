using DecisionSpark.Models.Runtime;

namespace DecisionSpark.Services;

public interface ISessionStore
{
    Task<DecisionSession?> GetAsync(string sessionId);
    Task SaveAsync(DecisionSession session);
    Task<bool> ExistsAsync(string sessionId);
}

public class InMemorySessionStore : ISessionStore
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, DecisionSession> _sessions = new();
    private readonly ILogger<InMemorySessionStore> _logger;

    public InMemorySessionStore(ILogger<InMemorySessionStore> logger)
    {
        _logger = logger;
    }

    public Task<DecisionSession?> GetAsync(string sessionId)
    {
        _logger.LogDebug("Retrieving session {SessionId}", sessionId);
        _sessions.TryGetValue(sessionId, out var session);
        return Task.FromResult(session);
    }

    public Task SaveAsync(DecisionSession session)
    {
        session.LastUpdatedUtc = DateTime.UtcNow;
        _sessions[session.SessionId] = session;
     _logger.LogDebug("Saved session {SessionId}", session.SessionId);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string sessionId)
    {
        return Task.FromResult(_sessions.ContainsKey(sessionId));
    }
}
