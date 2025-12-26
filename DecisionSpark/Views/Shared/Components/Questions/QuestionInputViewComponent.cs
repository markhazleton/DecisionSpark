using DecisionSpark.ViewModels.Question;
using Microsoft.AspNetCore.Mvc;

namespace DecisionSpark.Views.Shared.Components.Questions;

/// <summary>
/// View component that normalizes question data for rendering specific input controls.
/// Routes to the appropriate partial based on InputType.
/// </summary>
public class QuestionInputViewComponent : ViewComponent
{
    private readonly ILogger<QuestionInputViewComponent> _logger;

    public QuestionInputViewComponent(ILogger<QuestionInputViewComponent> logger)
    {
        _logger = logger;
    }

    public IViewComponentResult Invoke(QuestionViewModel model)
    {
        if (model == null)
        {
            _logger.LogWarning("[QuestionInputViewComponent] Null model provided");
            return Content(string.Empty);
        }

        _logger.LogDebug(
            "[QuestionInputViewComponent] Rendering {InputType} question for trait '{TraitId}'",
            model.InputType, model.Id);

        var viewName = model.InputType switch
        {
            QuestionInputType.SingleSelect => "_SingleSelectQuestion",
            QuestionInputType.MultiSelect => "_MultiSelectQuestion",
            _ => "_TextQuestion"
        };

        return View(viewName, model);
    }
}
