namespace DecisionSpark.Models.Api;

public class StartRequest
{
    // Empty for now, may extend to allow initial trait values
}

public class NextRequest
{
    public string? UserInput { get; set; }
    public List<int>? SelectedOptionIds { get; set; }
    public List<string>? SelectedOptionTexts { get; set; }
}
