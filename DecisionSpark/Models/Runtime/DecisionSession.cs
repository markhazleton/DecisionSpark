namespace DecisionSpark.Models.Runtime;

public class DecisionSession
{
    public string SessionId { get; set; } = string.Empty;
    public string SpecId { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public Dictionary<string, object> KnownTraits { get; set; } = new();
    public string? AwaitingTraitKey { get; set; }
    public bool IsComplete { get; set; }
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastUpdatedUtc { get; set; } = DateTime.UtcNow;
    public int ClarifierAttempts { get; set; }
    public string? PendingClarifierTraitKey { get; set; }
    public int RetryAttempt { get; set; }
}
