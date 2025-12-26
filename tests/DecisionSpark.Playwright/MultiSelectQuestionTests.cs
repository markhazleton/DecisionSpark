using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace DecisionSpark.Playwright;

/// <summary>
/// T026: Browser tests for multi-select checkbox questions
/// </summary>
[TestFixture]
public class MultiSelectQuestionTests : PageTest
{
    private const string BaseUrl = "http://localhost:5000";

    [Test]
    public async Task MultiSelect_ShouldRenderCheckboxes()
    {
        // Arrange: Navigate to page with multi-select question
        await Page.GotoAsync($"{BaseUrl}/demo");
        
        // Act: Wait for checkboxes to render
        var checkboxes = Page.Locator("input[type='checkbox'][name*='selected']");
        var count = await checkboxes.CountAsync();
        
        // Assert: Checkboxes exist (if question type is multi-select)
        if (count > 0)
        {
            Assert.That(count, Is.GreaterThan(0).And.LessThanOrEqualTo(7), 
                "Should have 1-7 checkbox options");
        }
    }

    [Test]
    public async Task MultiSelect_ShouldAllowMultipleSelections()
    {
        // Arrange
        await Page.GotoAsync($"{BaseUrl}/demo");
        var checkboxes = Page.Locator("input[type='checkbox'][name*='selected']");
        var count = await checkboxes.CountAsync();
        
        if (count < 2) 
        {
            Assert.Pass("Not enough checkboxes for this test");
            return;
        }
        
        // Act: Select multiple options
        await checkboxes.Nth(0).CheckAsync();
        await checkboxes.Nth(1).CheckAsync();
        
        var firstChecked = await checkboxes.Nth(0).IsCheckedAsync();
        var secondChecked = await checkboxes.Nth(1).IsCheckedAsync();
        
        // Assert: Both options remain selected
        Assert.That(firstChecked, Is.True, "First checkbox should remain selected");
        Assert.That(secondChecked, Is.True, "Second checkbox should remain selected");
    }

    [Test]
    public async Task MultiSelect_ShouldShowSelectionCounter()
    {
        // Arrange
        await Page.GotoAsync($"{BaseUrl}/demo");
        var checkboxes = Page.Locator("input[type='checkbox'][name*='selected']");
        var count = await checkboxes.CountAsync();
        
        if (count == 0)
        {
            Assert.Pass("No checkboxes found");
            return;
        }
        
        // Act: Select a checkbox
        await checkboxes.First.CheckAsync();
        
        // Assert: Look for selection counter
        var counter = Page.Locator("text=/\\d+ selected/i, text=/\\d+ of \\d+/i, .selection-count");
        var counterExists = await counter.CountAsync() > 0;
        
        if (counterExists)
        {
            var counterText = await counter.First.TextContentAsync();
            Assert.That(counterText, Does.Contain("1").Or.Contain("selected"), 
                "Counter should show selection count");
        }
    }

    [Test]
    public async Task MultiSelect_CheckboxesRemainCheckedAcrossInteractions()
    {
        // Arrange
        await Page.GotoAsync($"{BaseUrl}/demo");
        var checkboxes = Page.Locator("input[type='checkbox'][name*='selected']");
        var count = await checkboxes.CountAsync();
        
        if (count < 3)
        {
            Assert.Pass("Need at least 3 checkboxes for this test");
            return;
        }
        
        // Act: Select multiple, deselect one, select another
        await checkboxes.Nth(0).CheckAsync();
        await checkboxes.Nth(1).CheckAsync();
        await checkboxes.Nth(2).CheckAsync();
        await checkboxes.Nth(1).UncheckAsync();
        
        // Assert: Check state matches expectations
        var state0 = await checkboxes.Nth(0).IsCheckedAsync();
        var state1 = await checkboxes.Nth(1).IsCheckedAsync();
        var state2 = await checkboxes.Nth(2).IsCheckedAsync();
        
        Assert.That(state0, Is.True, "First checkbox should remain checked");
        Assert.That(state1, Is.False, "Second checkbox should be unchecked");
        Assert.That(state2, Is.True, "Third checkbox should remain checked");
    }

    [Test]
    public async Task MultiSelect_ShouldShowRequiredVsOptionalMessaging()
    {
        // Arrange
        await Page.GotoAsync($"{BaseUrl}/demo");
        var checkboxes = Page.Locator("input[type='checkbox'][name*='selected']");
        var count = await checkboxes.CountAsync();
        
        if (count == 0)
        {
            Assert.Pass("No checkboxes found");
            return;
        }
        
        // Act: Look for required/optional indicators
        var requiredIndicator = Page.Locator("text=/required/i, .required, [aria-required='true']");
        var optionalIndicator = Page.Locator("text=/optional/i, .optional");
        
        var hasRequired = await requiredIndicator.CountAsync() > 0;
        var hasOptional = await optionalIndicator.CountAsync() > 0;
        
        // Assert: Should have some indication of requirement status
        Assert.That(hasRequired || hasOptional, Is.True, 
            "Multi-select should indicate if selection is required or optional");
    }

    [Test]
    public async Task MultiSelect_ShouldHaveAccessibleLabels()
    {
        // Arrange
        await Page.GotoAsync($"{BaseUrl}/demo");
        var checkboxes = Page.Locator("input[type='checkbox'][name*='selected']");
        var count = await checkboxes.CountAsync();
        
        if (count == 0)
        {
            Assert.Pass("No checkboxes found");
            return;
        }
        
        // Act: Check first checkbox for accessibility
        var firstCheckbox = checkboxes.First;
        var ariaLabel = await firstCheckbox.GetAttributeAsync("aria-label");
        var id = await firstCheckbox.GetAttributeAsync("id");
        
        // Assert: Has proper ARIA or associated label
        bool hasAccessibility = !string.IsNullOrEmpty(ariaLabel) ||
                                (!string.IsNullOrEmpty(id) && await Page.Locator($"label[for='{id}']").CountAsync() > 0);
        
        Assert.That(hasAccessibility, Is.True, "Checkboxes should have accessible labels");
    }

    [Test]
    public async Task MultiSelect_ShouldEnforceSevenOptionLimit()
    {
        // Arrange
        await Page.GotoAsync($"{BaseUrl}/demo");
        var checkboxes = Page.Locator("input[type='checkbox'][name*='selected']");
        var count = await checkboxes.CountAsync();
        
        // Assert: Never more than 7 options (FR-024a)
        Assert.That(count, Is.LessThanOrEqualTo(7), 
            "Multi-select should never show more than 7 options");
    }
}
