namespace DecisionSpark.Common;

/// <summary>
/// Application-wide constants
/// </summary>
public static class Constants
{
    /// <summary>
    /// HTTP header name for API key authentication
    /// </summary>
    public const string API_KEY_HEADER = "X-API-KEY";

    /// <summary>
    /// Configuration key for the API key
    /// </summary>
    public const string API_KEY_CONFIG = "DecisionEngine:ApiKey";

    /// <summary>
    /// Maximum allowed user input size in bytes (2KB)
    /// </summary>
    public const int MAX_INPUT_SIZE = 2048;

    /// <summary>
    /// Rule type constants
    /// </summary>
    public static class RuleTypes
    {
        public const string IMMEDIATE_SELECT = "IMMEDIATE_SELECT";
        public const string OUTCOME_RULE = "OUTCOME_RULE";
    }

    /// <summary>
    /// Error codes returned by the API
    /// </summary>
    public static class ErrorCodes
    {
        public const string INVALID_API_KEY = "INVALID_API_KEY";
        public const string INVALID_INPUT = "INVALID_INPUT";
        public const string SESSION_NOT_FOUND = "SESSION_NOT_FOUND";
        public const string SESSION_STATE_INVALID = "SESSION_STATE_INVALID";
        public const string INPUT_TOO_LARGE = "INPUT_TOO_LARGE";
        public const string INTERNAL_ERROR = "INTERNAL_ERROR";
    }
}
