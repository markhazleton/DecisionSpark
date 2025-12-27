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
            .SetValidator(new MetadataValidator()!);

        // Validate Traits exist and are valid
        RuleFor(x => x.Traits)
            .NotEmpty().WithErrorCode("SPEC008").WithMessage("At least one trait is required");

        RuleForEach(x => x.Traits)
            .SetValidator(new TraitValidator());

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
/// Validates individual trait definitions.
/// </summary>
public class TraitValidator : AbstractValidator<TraitDefinition>
{
    public TraitValidator()
    {
        RuleFor(x => x.Key)
            .NotEmpty().WithErrorCode("T001").WithMessage("Key is required")
            .Matches(@"^[a-zA-Z0-9_-]+$").WithErrorCode("T002").WithMessage("Key must contain only letters, numbers, hyphens, and underscores");

        RuleFor(x => x.AnswerType)
            .NotEmpty().WithErrorCode("T003").WithMessage("AnswerType is required")
            .Must(t => new[] { "choice", "multi", "text", "number", "date" }.Contains(t))
            .WithErrorCode("T004").WithMessage("AnswerType must be one of: choice, multi, text, number, date");

        RuleFor(x => x.QuestionText)
            .NotEmpty().WithErrorCode("T005").WithMessage("QuestionText is required")
            .MaximumLength(500).WithErrorCode("T006").WithMessage("QuestionText must not exceed 500 characters");

        When(x => x.Comment != null, () =>
        {
            RuleFor(x => x.Comment)
                .MaximumLength(500).WithErrorCode("T007").WithMessage("Comment must not exceed 500 characters");
        });

        When(x => x.AnswerType == "choice" || x.AnswerType == "multi", () =>
        {
            RuleFor(x => x.Options)
                .NotEmpty().WithErrorCode("T008").WithMessage("Options are required for choice and multi answer types")
                .Must(options => options != null && options.Distinct().Count() == options.Count)
                .WithErrorCode("T009").WithMessage("Option keys must be unique within a trait");
        });
    }
}

/// <summary>
/// Validates outcomes.
/// </summary>
public class OutcomeValidator : AbstractValidator<OutcomeDefinition>
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

        RuleFor(x => x.Traits)
            .Must(traits => traits.Select(t => t.Key).Distinct().Count() == traits.Count)
            .WithErrorCode("QSPEC001")
            .WithMessage("Trait keys must be unique across the entire spec");

        RuleFor(x => x.Outcomes)
            .Must(outcomes => outcomes.Select(o => o.OutcomeId).Distinct().Count() == outcomes.Count)
            .WithErrorCode("QSPEC002")
            .WithMessage("Outcome IDs must be unique across the entire spec");
    }
}

