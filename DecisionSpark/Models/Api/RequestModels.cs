namespace DecisionSpark.Models.Api;

public class StartRequest
{
    // Empty for now, may extend to allow initial trait values
}

public class NextRequest
{
    private string? _userInput;
    
    public string? UserInput 
    { 
        get => _userInput;
        set
        {
            _userInput = value;
            Console.WriteLine($"[NextRequest] UserInput property SET to: '{value ?? "NULL"}' (Length: {value?.Length ?? 0})");
     }
    }
  
    public List<int>? SelectedOptionIds { get; set; }
    public List<string>? SelectedOptionTexts { get; set; }
}
