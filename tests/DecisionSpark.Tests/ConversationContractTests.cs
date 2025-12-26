using DecisionSpark.Models.Api;
using System.Text.Json;
using Xunit;
using FluentAssertions;

namespace DecisionSpark.Tests;

/// <summary>
/// T013: Contract and backward compatibility tests for API responses
/// </summary>
public class ConversationContractTests
{
    [Fact]
    public void QuestionDto_WithNewFields_ShouldSerializeCorrectly()
    {
        // Arrange
        var question = new QuestionDto
        {
            Id = "test_trait",
            Source = "TEST_SPEC",
            Text = "How many people?",
            Type = "single-select",
            AllowFreeText = true,
            IsFreeText = false,
            AllowMultiSelect = false,
            IsMultiSelect = false,
            RetryAttempt = null,
            Options = new List<QuestionOptionDto>
            {
                new QuestionOptionDto
                {
                    Id = "option-1",
                    Label = "One person",
                    Value = "1",
                    IsNegative = false,
                    IsDefault = false
                }
            },
            Metadata = new QuestionMetadataDto
            {
                Confidence = 0.9f,
                ValidationHints = new List<string>()
            }
        };

        // Act
        var json = JsonSerializer.Serialize(question);
        var deserialized = JsonSerializer.Deserialize<QuestionDto>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Id.Should().Be("test_trait");
        deserialized.Type.Should().Be("single-select");
        deserialized.Options.Should().HaveCount(1);
        deserialized.Metadata.Should().NotBeNull();
    }

    [Fact]
    public void NextRequest_WithSelectedOptionIds_ShouldDeserializeCorrectly()
    {
        // Arrange
        var json = @"{
            ""user_input"": ""some text"",
            ""selected_option_ids"": [""option-1"", ""option-2""]
        }";

        // Act
        var request = JsonSerializer.Deserialize<NextRequest>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Assert
        request.Should().NotBeNull();
        request!.UserInput.Should().Be("some text");
        request.SelectedOptionIds.Should().NotBeNull();
        request.SelectedOptionIds.Should().HaveCount(2);
        request.SelectedOptionIds.Should().Contain(new[] { "option-1", "option-2" });
    }

    [Fact]
    public void StartResponse_WithLegacyClient_ShouldIgnoreNewFields()
    {
        // Arrange: Simulate a legacy client that doesn't know about new fields
        var json = @"{
            ""is_complete"": false,
            ""texts"": [""Thanks! One quick question.""],
            ""question"": {
                ""id"": ""test_trait"",
                ""source"": ""TEST_SPEC"",
                ""text"": ""How many people?"",
                ""type"": ""text"",
                ""allow_free_text"": true,
                ""is_free_text"": true,
                ""options"": [],
                ""metadata"": {
                    ""confidence"": 0.9
                }
            },
            ""next_url"": ""https://localhost:5001/conversation/abc123/next""
        }";

        // Act: Deserialize as if we're a legacy client (ignoring unknown properties)
        var response = JsonSerializer.Deserialize<StartResponse>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        // Assert: Legacy fields still work
        response.Should().NotBeNull();
        response!.IsComplete.Should().BeFalse();
        response.Question.Should().NotBeNull();
        response.Question!.Id.Should().Be("test_trait");
        response.Question.Text.Should().Be("How many people?");
    }

    [Fact]
    public void QuestionOptionDto_ShouldSerializeWithAllFields()
    {
        // Arrange
        var option = new QuestionOptionDto
        {
            Id = "outdoor-activities",
            Label = "Outdoor activities",
            Value = "OUTDOOR",
            IsNegative = false,
            IsDefault = true,
            Confidence = 0.85f
        };

        // Act
        var json = JsonSerializer.Serialize(option);
        var deserialized = JsonSerializer.Deserialize<QuestionOptionDto>(json);

        // Assert
        deserialized.Should().NotBeNull();
        deserialized!.Id.Should().Be("outdoor-activities");
        deserialized.Label.Should().Be("Outdoor activities");
        deserialized.Value.Should().Be("OUTDOOR");
        deserialized.IsNegative.Should().BeFalse();
        deserialized.IsDefault.Should().BeTrue();
        deserialized.Confidence.Should().Be(0.85f);
    }
}
