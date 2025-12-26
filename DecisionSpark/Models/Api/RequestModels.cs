using System.Text.Json.Serialization;

namespace DecisionSpark.Models.Api;

public class StartRequest
{
    [JsonPropertyName("spec_id")]
    public string? SpecId { get; set; }
}

public class NextRequest
{
    private string? _userInput;
    
    [JsonPropertyName("user_input")]
    public string? UserInput 
    { 
        get => _userInput;
        set
        {
            _userInput = value;
            Console.WriteLine($"[NextRequest] UserInput property SET to: '{value ?? "NULL"}' (Length: {value?.Length ?? 0})");
        }
    }
    
    [JsonPropertyName("selected_option_ids")]
    public string[]? SelectedOptionIds { get; set; }
    
    [JsonPropertyName("selected_option_texts")]
    public string[]? SelectedOptionTexts { get; set; }
}
