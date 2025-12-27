using DecisionSpark.Core.Models.Spec;
using FluentValidation;

namespace DecisionSpark.Core.Services.Validation;

/// <summary>
/// Validates DecisionSpec documents for completeness and correctness.
/// </summary>
public class DecisionSpecValidator : AbstractValidator<DecisionSpecDocument>
{
    public DecisionSpecValidator()
    {
        RuleFor(x => x.SpecId)
            .NotEmpty().WithErrorCode("SPEC001").WithMessage("SpecId is required")
            .Matches(@"^[a-zA-Z0-9_-]+$").WithErrorCode("SPEC002").WithMessage("SpecId must contain only letters, numbers, hyphens, and underscores");

        RuleFor(x => x.Version)
            .NotEmpty().WithErrorCode("SPEC003").WithMessage("Version is required")
            .Matches(@"^\d+\.\d+\.\d+$").WithErrorCode("SPEC004").WithMessage("Version must follow semantic versioning (e.g., 1.0.0)");

        RuleFor(x => x.Status)
            .NotEmpty().WithErrorCode("SPEC005").WithMessage("Status is required")
            .Must(s => new[] { "Draft", "InReview", "Published", "Retired" }.Contains(s))
            .WithErrorCode("SPEC006").WithMessage("Status must be one of: Draft, InReview, Published, Retired");

        RuleFor(x => x.Metadata)
            .NotNull().WithErrorCode("SPEC007").WithMessage("Metadata is required")
            .SetValidator(new MetadataValidator());

        // Validate Questions exist and are valid
        RuleFor(x => x.Questions)
            .NotEmpty().WithErrorCode("SPEC008").WithMessage("At least one question is required");

        RuleForEach(x => x.Questions)
            .SetValidator(new QuestionValidator());

        // Validate Outcomes exist and are valid
        RuleFor(x => x.Outcomes)
            .NotEmpty().WithErrorCode("SPEC009").WithMessage("At least one outcome is required");

        RuleForEach(x => x.Outcomes)
            .SetValidator(new OutcomeValidator());
    }
}

/// <summary>
/// Validates DecisionSpec metadata.
/// </summary>
public class MetadataValidator : AbstractValidator<DecisionSpecMetadata>
{
    public MetadataValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithErrorCode("META001").WithMessage("Name is required")
            .MaximumLength(200).WithErrorCode("META002").WithMessage("Name must not exceed 200 characters");

        RuleFor(x => x.Owner)
            .NotEmpty().WithErrorCode("META003").WithMessage("Owner is required");
    }
}

/// <summary>
/// Validates individual questions.
/// </summary>
public class QuestionValidator : AbstractValidator<Question>
{
    public QuestionValidator()
    {
        RuleFor(x => x.QuestionId)
            .NotEmpty().WithErrorCode("Q001").WithMessage("QuestionId is required")
            .Matches(@"^[a-zA-Z0-9_-]+$").WithErrorCode("Q002").WithMessage("QuestionId must contain only letters, numbers, hyphens, and underscores");

        RuleFor(x => x.Type)
            .NotEmpty().WithErrorCode("Q003").WithMessage("Question Type is required")
            .Must(t => new[] { "SingleSelect", "MultiSelect", "Text" }.Contains(t))
            .WithErrorCode("Q004").WithMessage("Question Type must be one of: SingleSelect, MultiSelect, Text");

        RuleFor(x => x.Prompt)
            .NotEmpty().WithErrorCode("Q005").WithMessage("Prompt is required")
            .MaximumLength(500).WithErrorCode("Q006").WithMessage("Prompt must not exceed 500 characters");

        When(x => x.Type == "SingleSelect" || x.Type == "MultiSelect", () =>
        {
            RuleFor(x => x.Options)
                .NotEmpty().WithErrorCode("Q007").WithMessage("Options are required for SingleSelect and MultiSelect questions")
                .Must(o => o.Select(x => x.OptionId).Distinct().Count() == o.Count)
                .WithErrorCode("Q008").WithMessage("Option IDs must be unique within a question");
        });

        RuleForEach(x => x.Options)
            .SetValidator(new OptionValidator());
    }
}

/// <summary>
/// Validates question options.
/// </summary>
public class OptionValidator : AbstractValidator<Option>
{
    public OptionValidator()
    {
        RuleFor(x => x.OptionId)
            .NotEmpty().WithErrorCode("OPT001").WithMessage("OptionId is required");

        RuleFor(x => x.Label)
            .NotEmpty().WithErrorCode("OPT002").WithMessage("Label is required")
            .MaximumLength(200).WithErrorCode("OPT003").WithMessage("Label must not exceed 200 characters");

        RuleFor(x => x.Value)
            .NotEmpty().WithErrorCode("OPT004").WithMessage("Value is required");
    }
}

/// <summary>
/// Validates outcomes.
/// </summary>
public class OutcomeValidator : AbstractValidator<Outcome>
{
    public OutcomeValidator()
    {
        RuleFor(x => x.OutcomeId)
            .NotEmpty().WithErrorCode("OUT001").WithMessage("OutcomeId is required");

        RuleFor(x => x.SelectionRules)
            .NotEmpty().WithErrorCode("OUT002").WithMessage("At least one selection rule is required");

        RuleFor(x => x.DisplayCards)
            .NotEmpty().WithErrorCode("OUT003").WithMessage("At least one display card is required");
    }
}

/// <summary>
/// Extended validator for question-type DecisionSpecs with questions and outcomes.
/// </summary>
public class QuestionBasedSpecValidator : AbstractValidator<DecisionSpecDocument>
{
    public QuestionBasedSpecValidator()
    {
        Include(new DecisionSpecValidator());

        RuleFor(x => x.Questions)
            .Must(questions => questions.Select(q => q.QuestionId).Distinct().Count() == questions.Count)
            .WithErrorCode("QSPEC001")
            .WithMessage("Question IDs must be unique across the entire spec");

        RuleFor(x => x.Outcomes)
            .Must(outcomes => outcomes.Select(o => o.OutcomeId).Distinct().Count() == outcomes.Count)
            .WithErrorCode("QSPEC002")
            .WithMessage("Outcome IDs must be unique across the entire spec");
    }
}

