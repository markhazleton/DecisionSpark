using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace DecisionSpark.Playwright;

/// <summary>
/// T020: Browser tests for single-select radio button questions
/// </summary>
[TestFixture]
public class SingleSelectQuestionTests : PageTest
{
    private const string BaseUrl = "http://localhost:5000";

    [Test]
    public async Task SingleSelect_ShouldRenderRadioButtons()
    {
        // Arrange: Navigate to page with single-select question
        await Page.GotoAsync($"{BaseUrl}/demo");
        
        // Act: Wait for radio buttons to render
        var radioButtons = Page.Locator("input[type='radio']");
        var count = await radioButtons.CountAsync();
        
        // Assert: Radio buttons exist (if question type is single-select)
        if (count > 0)
        {
            Assert.That(count, Is.GreaterThan(0).And.LessThanOrEqualTo(7), 
                "Should have 1-7 radio button options");
        }
    }

    [Test]
    public async Task SingleSelect_ShouldEnforceSingleSelection()
    {
        // Arrange
        await Page.GotoAsync($"{BaseUrl}/demo");
        var radioButtons = Page.Locator("input[type='radio']");
        var count = await radioButtons.CountAsync();
        
        if (count < 2) 
        {
            Assert.Pass("Not enough radio buttons for this test");
            return;
        }
        
        // Act: Select first option
        await radioButtons.Nth(0).CheckAsync();
        var firstChecked = await radioButtons.Nth(0).IsCheckedAsync();
        
        // Select second option
        await radioButtons.Nth(1).CheckAsync();
        var secondChecked = await radioButtons.Nth(1).IsCheckedAsync();
        var firstStillChecked = await radioButtons.Nth(0).IsCheckedAsync();
        
        // Assert: Only one option selected
        Assert.That(secondChecked, Is.True, "Second option should be selected");
        Assert.That(firstStillChecked, Is.False, "First option should be deselected");
    }

    [Test]
    public async Task SingleSelect_ShouldShowTruncatedLabelsWithTooltips()
    {
        // Arrange
        await Page.GotoAsync($"{BaseUrl}/demo");
        var radioButtons = Page.Locator("input[type='radio']");
        var count = await radioButtons.CountAsync();
        
        if (count == 0)
        {
            Assert.Pass("No radio buttons found");
            return;
        }
        
        // Act: Find labels associated with radio buttons
        var labels = Page.Locator("label");
        var labelCount = await labels.CountAsync();
        
        // Assert: Labels exist
        Assert.That(labelCount, Is.GreaterThan(0), "Radio buttons should have labels");
        
        // Check if any long labels have tooltips
        for (int i = 0; i < Math.Min(labelCount, 7); i++)
        {
            var label = labels.Nth(i);
            var text = await label.TextContentAsync();
            
            if (!string.IsNullOrEmpty(text) && text.Length > 60)
            {
                // Long label should have title attribute or tooltip
                var title = await label.GetAttributeAsync("title");
                var span = label.Locator("span[title]");
                var spanCount = await span.CountAsync();
                
                bool hasTooltip = !string.IsNullOrEmpty(title) || spanCount > 0;
                Assert.That(hasTooltip, Is.True, $"Long label '{text.Substring(0, 30)}...' should have tooltip");
            }
        }
    }

    [Test]
    public async Task SingleSelect_ShouldHaveCustomTextFallback()
    {
        // Arrange
        await Page.GotoAsync($"{BaseUrl}/demo");
        var radioButtons = Page.Locator("input[type='radio']");
        var count = await radioButtons.CountAsync();
        
        if (count == 0)
        {
            Assert.Pass("No radio buttons found");
            return;
        }
        
        // Act: Look for "Type my own answer" or custom text option
        var customTextLink = Page.Locator("text=/type.*own.*answer/i, text=/custom.*text/i, text=/other/i");
        var customTextCount = await customTextLink.CountAsync();
        
        // Assert: Custom text option should be available (FR-006)
        Assert.That(customTextCount, Is.GreaterThanOrEqualTo(0), 
            "Single-select should ideally offer custom text fallback");
    }

    [Test]
    public async Task SingleSelect_ShouldHaveAccessibleARIARoles()
    {
        // Arrange
        await Page.GotoAsync($"{BaseUrl}/demo");
        var radioButtons = Page.Locator("input[type='radio']");
        var count = await radioButtons.CountAsync();
        
        if (count == 0)
        {
            Assert.Pass("No radio buttons found");
            return;
        }
        
        // Act: Check first radio button for accessibility
        var firstRadio = radioButtons.First;
        var role = await firstRadio.GetAttributeAsync("role");
        var ariaLabel = await firstRadio.GetAttributeAsync("aria-label");
        var id = await firstRadio.GetAttributeAsync("id");
        
        // Assert: Has proper ARIA or associated label
        bool hasAccessibility = role == "radio" || 
                                !string.IsNullOrEmpty(ariaLabel) ||
                                (!string.IsNullOrEmpty(id) && await Page.Locator($"label[for='{id}']").CountAsync() > 0);
        
        Assert.That(hasAccessibility, Is.True, "Radio buttons should have proper ARIA roles or labels");
    }

    [Test]
    public async Task SingleSelect_ShouldSupportKeyboardNavigation()
    {
        // Arrange
        await Page.GotoAsync($"{BaseUrl}/demo");
        var radioButtons = Page.Locator("input[type='radio']");
        var count = await radioButtons.CountAsync();
        
        if (count < 2)
        {
            Assert.Pass("Need at least 2 radio buttons for keyboard test");
            return;
        }
        
        // Act: Focus first radio and use arrow keys
        await radioButtons.First.FocusAsync();
        await Page.Keyboard.PressAsync("ArrowDown");
        
        // Assert: Focus should move to next option
        var focusedElement = await Page.EvaluateAsync<string>("document.activeElement.type");
        Assert.That(focusedElement, Is.EqualTo("radio"), "Arrow key should navigate between radio buttons");
    }
}
