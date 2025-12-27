using DecisionSpark.Areas.Admin.ViewModels.DecisionSpecs;
using FluentValidation;

namespace DecisionSpark.Areas.Admin.ViewModels.DecisionSpecs;

/// <summary>
/// Validator for DecisionSpec edit view model.
/// </summary>
public class DecisionSpecEditViewModelValidator : AbstractValidator<DecisionSpecEditViewModel>
{
    public DecisionSpecEditViewModelValidator()
    {
        RuleFor(x => x.SpecId)
            .NotEmpty()
            .WithMessage("Spec ID is required")
            .WithErrorCode("SPEC_ID_REQUIRED")
            .Matches(@"^[A-Za-z0-9_-]+$")
            .WithMessage("Spec ID must contain only letters, numbers, underscores, and hyphens")
            .WithErrorCode("SPEC_ID_INVALID_FORMAT");

        RuleFor(x => x.Version)
            .NotEmpty()
            .WithMessage("Version is required")
            .WithErrorCode("VERSION_REQUIRED")
            .Matches(@"^\d+\.\d+\.\d+$")
            .WithMessage("Version must follow format: major.minor.patch (e.g., 2025.12.1)")
            .WithErrorCode("VERSION_INVALID_FORMAT");

        RuleFor(x => x.Status)
            .NotEmpty()
            .WithMessage("Status is required")
            .WithErrorCode("STATUS_REQUIRED")
            .Must(status => new[] { "Draft", "InReview", "Published", "Retired" }.Contains(status))
            .WithMessage("Status must be one of: Draft, InReview, Published, Retired")
            .WithErrorCode("STATUS_INVALID");

        RuleFor(x => x.Metadata)
            .NotNull()
            .WithMessage("Metadata is required")
            .WithErrorCode("METADATA_REQUIRED")
            .SetValidator(new DecisionSpecMetadataViewModelValidator());

        RuleFor(x => x.Questions)
            .NotEmpty()
            .WithMessage("At least one question is required")
            .WithErrorCode("QUESTIONS_REQUIRED")
            .Must(questions => questions.Count >= 1)
            .WithMessage("At least one question is required")
            .WithErrorCode("QUESTIONS_MIN_COUNT");

        RuleForEach(x => x.Questions)
            .SetValidator(new QuestionViewModelValidator());

        RuleFor(x => x.Outcomes)
            .NotEmpty()
            .WithMessage("At least one outcome is required")
            .WithErrorCode("OUTCOMES_REQUIRED")
            .Must(outcomes => outcomes.Count >= 1)
            .WithMessage("At least one outcome is required")
            .WithErrorCode("OUTCOMES_MIN_COUNT");

        RuleForEach(x => x.Outcomes)
            .SetValidator(new OutcomeViewModelValidator());

        // Cross-field validation: Question IDs must be unique
        RuleFor(x => x.Questions)
            .Must(questions => questions.Select(q => q.QuestionId).Distinct().Count() == questions.Count)
            .WithMessage("Question IDs must be unique")
            .WithErrorCode("QUESTION_IDS_DUPLICATE");

        // Cross-field validation: Outcome IDs must be unique
        RuleFor(x => x.Outcomes)
            .Must(outcomes => outcomes.Select(o => o.OutcomeId).Distinct().Count() == outcomes.Count)
            .WithMessage("Outcome IDs must be unique")
            .WithErrorCode("OUTCOME_IDS_DUPLICATE");
    }
}

/// <summary>
/// Validator for DecisionSpec metadata view model.
/// </summary>
public class DecisionSpecMetadataViewModelValidator : AbstractValidator<DecisionSpecMetadataViewModel>
{
    public DecisionSpecMetadataViewModelValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .WithMessage("Name is required")
            .WithErrorCode("NAME_REQUIRED")
            .MaximumLength(200)
            .WithMessage("Name cannot exceed 200 characters")
            .WithErrorCode("NAME_TOO_LONG");

        RuleFor(x => x.Description)
            .MaximumLength(1000)
            .WithMessage("Description cannot exceed 1000 characters")
            .WithErrorCode("DESCRIPTION_TOO_LONG");
    }
}

/// <summary>
/// Validator for Question view model.
/// </summary>
public class QuestionViewModelValidator : AbstractValidator<QuestionViewModel>
{
    public QuestionViewModelValidator()
    {
        RuleFor(x => x.QuestionId)
            .NotEmpty()
            .WithMessage("Question ID is required")
            .WithErrorCode("QUESTION_ID_REQUIRED")
            .Matches(@"^[A-Za-z0-9_-]+$")
            .WithMessage("Question ID must contain only letters, numbers, underscores, and hyphens")
            .WithErrorCode("QUESTION_ID_INVALID_FORMAT");

        RuleFor(x => x.Type)
            .NotEmpty()
            .WithMessage("Question type is required")
            .WithErrorCode("QUESTION_TYPE_REQUIRED")
            .Must(type => new[] { "SingleSelect", "MultiSelect", "Text", "Number", "Date" }.Contains(type))
            .WithMessage("Question type must be one of: SingleSelect, MultiSelect, Text, Number, Date")
            .WithErrorCode("QUESTION_TYPE_INVALID");

        RuleFor(x => x.Prompt)
            .NotEmpty()
            .WithMessage("Question prompt is required")
            .WithErrorCode("QUESTION_PROMPT_REQUIRED")
            .MaximumLength(500)
            .WithMessage("Prompt cannot exceed 500 characters")
            .WithErrorCode("QUESTION_PROMPT_TOO_LONG");

        RuleFor(x => x.HelpText)
            .MaximumLength(1000)
            .WithMessage("Help text cannot exceed 1000 characters")
            .WithErrorCode("QUESTION_HELP_TEXT_TOO_LONG")
            .When(x => !string.IsNullOrWhiteSpace(x.HelpText));

        // Options required for select-type questions
        RuleFor(x => x.Options)
            .NotEmpty()
            .WithMessage("At least one option is required for select-type questions")
            .WithErrorCode("QUESTION_OPTIONS_REQUIRED")
            .When(x => x.Type == "SingleSelect" || x.Type == "MultiSelect");

        RuleForEach(x => x.Options)
            .SetValidator(new OptionViewModelValidator())
            .When(x => x.Options.Any());

        // Option IDs must be unique within a question
        RuleFor(x => x.Options)
            .Must(options => options.Select(o => o.OptionId).Distinct().Count() == options.Count)
            .WithMessage("Option IDs must be unique within a question")
            .WithErrorCode("OPTION_IDS_DUPLICATE")
            .When(x => x.Options.Any());
    }
}

/// <summary>
/// Validator for Option view model.
/// </summary>
public class OptionViewModelValidator : AbstractValidator<OptionViewModel>
{
    public OptionViewModelValidator()
    {
        RuleFor(x => x.OptionId)
            .NotEmpty()
            .WithMessage("Option ID is required")
            .WithErrorCode("OPTION_ID_REQUIRED");

        RuleFor(x => x.Label)
            .NotEmpty()
            .WithMessage("Option label is required")
            .WithErrorCode("OPTION_LABEL_REQUIRED")
            .MaximumLength(200)
            .WithMessage("Label cannot exceed 200 characters")
            .WithErrorCode("OPTION_LABEL_TOO_LONG");
    }
}

/// <summary>
/// Validator for Outcome view model.
/// </summary>
public class OutcomeViewModelValidator : AbstractValidator<OutcomeViewModel>
{
    public OutcomeViewModelValidator()
    {
        RuleFor(x => x.OutcomeId)
            .NotEmpty()
            .WithMessage("Outcome ID is required")
            .WithErrorCode("OUTCOME_ID_REQUIRED")
            .Matches(@"^[A-Za-z0-9_-]+$")
            .WithMessage("Outcome ID must contain only letters, numbers, underscores, and hyphens")
            .WithErrorCode("OUTCOME_ID_INVALID_FORMAT");
    }
}
