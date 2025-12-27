using DecisionSpark.Areas.Admin.ViewModels.DecisionSpecs;
using DecisionSpark.Core.Persistence.Repositories;
using DecisionSpark.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace DecisionSpark.Areas.Admin.Controllers;

/// <summary>
/// Admin controller for managing DecisionSpecs through the web UI.
/// </summary>
[Area("Admin")]
[Route("Admin/DecisionSpecs")]
public class DecisionSpecsController : Controller
{
    private readonly IDecisionSpecRepository _repository;
    private readonly IOpenAIService _openAIService;
    private readonly ILogger<DecisionSpecsController> _logger;

    public DecisionSpecsController(
        IDecisionSpecRepository repository,
        IOpenAIService openAIService,
        ILogger<DecisionSpecsController> logger)
    {
        _repository = repository;
        _openAIService = openAIService;
        _logger = logger;
    }

    /// <summary>
    /// Displays the DecisionSpec catalog with search and filter capabilities.
    /// </summary>
    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] string? owner = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var summaries = await _repository.ListAsync(status, owner, search, cancellationToken);
            
            var viewModel = new DecisionSpecListViewModel
            {
                Items = summaries
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(s => new DecisionSpecSummaryViewModel
                    {
                        SpecId = s.SpecId,
                        Name = s.Name,
                        Status = s.Status,
                        Owner = s.Owner,
                        Version = s.Version,
                        UpdatedAt = s.UpdatedAt,
                        QuestionCount = s.QuestionCount,
                        HasUnverifiedDraft = s.HasUnverifiedDraft
                    })
                    .ToList(),
                Total = summaries.Count(),
                Page = page,
                PageSize = pageSize,
                SearchTerm = search,
                StatusFilter = status,
                OwnerFilter = owner
            };

            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading DecisionSpec catalog");
            TempData["Error"] = "Failed to load DecisionSpecs. Please try again.";
            return View(new DecisionSpecListViewModel());
        }
    }

    /// <summary>
    /// Displays the create form for a new DecisionSpec.
    /// </summary>
    [HttpGet("Create")]
    public IActionResult Create()
    {
        var viewModel = new DecisionSpecEditViewModel
        {
            Status = "Draft",
            Metadata = new DecisionSpecMetadataViewModel
            {
                Tags = new List<string>()
            },
            Questions = new List<QuestionViewModel>(),
            Outcomes = new List<OutcomeViewModel>()
        };

        return View("Edit", viewModel);
    }

    /// <summary>
    /// Displays the edit form for an existing DecisionSpec.
    /// </summary>
    [HttpGet("Edit/{specId}")]
    public async Task<IActionResult> Edit(
        string specId,
        [FromQuery] string? version = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _repository.GetAsync(specId, version, cancellationToken);
            
            if (result == null)
            {
                TempData["Error"] = $"DecisionSpec '{specId}' not found.";
                return RedirectToAction(nameof(Index));
            }

            var (doc, etag) = result.Value;

            var viewModel = new DecisionSpecEditViewModel
            {
                SpecId = doc.SpecId,
                Version = doc.Version,
                Status = doc.Status,
                ETag = etag,
                Metadata = new DecisionSpecMetadataViewModel
                {
                    Name = doc.Metadata.Name,
                    Description = doc.Metadata.Description,
                    Tags = doc.Metadata.Tags?.ToList() ?? new List<string>()
                },
                Questions = doc.Questions.Select(q => new QuestionViewModel
                {
                    QuestionId = q.QuestionId,
                    Type = q.Type,
                    Prompt = q.Prompt,
                    HelpText = q.HelpText,
                    Required = q.Required,
                    Options = q.Options.Select(o => new OptionViewModel
                    {
                        OptionId = o.OptionId,
                        Label = o.Label,
                        Value = o.Value,
                        NextQuestionId = o.NextQuestionId
                    }).ToList(),
                    Validation = q.Validation
                }).ToList(),
                Outcomes = doc.Outcomes.Select(o => new OutcomeViewModel
                {
                    OutcomeId = o.OutcomeId,
                    SelectionRules = o.SelectionRules?.ToList() ?? new List<string>()
                }).ToList()
            };

            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading DecisionSpec {SpecId} for editing", specId);
            TempData["Error"] = "Failed to load DecisionSpec. Please try again.";
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// Handles form submission for creating or updating a DecisionSpec.
    /// </summary>
    [HttpPost("Save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(
        DecisionSpecEditViewModel viewModel,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return View("Edit", viewModel);
        }

        try
        {
            var doc = MapToDocument(viewModel);
            
            if (string.IsNullOrWhiteSpace(viewModel.ETag))
            {
                // Create new spec
                var (created, etag) = await _repository.CreateAsync(doc, cancellationToken);
                
                _logger.LogInformation("Created DecisionSpec {SpecId} version {Version}", created.SpecId, created.Version);
                TempData["Success"] = $"DecisionSpec '{created.SpecId}' created successfully.";
                
                return RedirectToAction(nameof(Details), new { specId = created.SpecId });
            }
            else
            {
                // Update existing spec
                var result = await _repository.UpdateAsync(viewModel.SpecId, doc, viewModel.ETag, cancellationToken);
                
                if (result == null)
                {
                    TempData["Error"] = "DecisionSpec not found.";
                    return RedirectToAction(nameof(Index));
                }

                var (updated, etag) = result.Value;
                
                _logger.LogInformation("Updated DecisionSpec {SpecId} version {Version}", updated.SpecId, updated.Version);
                TempData["Success"] = $"DecisionSpec '{updated.SpecId}' updated successfully.";
                
                return RedirectToAction(nameof(Details), new { specId = updated.SpecId });
            }
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("ETag mismatch"))
        {
            _logger.LogWarning("Concurrency conflict while saving DecisionSpec {SpecId}", viewModel.SpecId);
            
            ModelState.AddModelError("", "This DecisionSpec has been modified by another user. Please review the changes and try again.");
            viewModel.ShowConcurrencyConflict = true;
            
            return View("Edit", viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving DecisionSpec {SpecId}", viewModel.SpecId);
            
            ModelState.AddModelError("", "An error occurred while saving. Please try again.");
            return View("Edit", viewModel);
        }
    }

    /// <summary>
    /// Displays detailed information about a DecisionSpec including audit history.
    /// </summary>
    [HttpGet("Details/{specId}")]
    public async Task<IActionResult> Details(
        string specId,
        [FromQuery] string? version = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _repository.GetAsync(specId, version, cancellationToken);
            
            if (result == null)
            {
                TempData["Error"] = $"DecisionSpec '{specId}' not found.";
                return RedirectToAction(nameof(Index));
            }

            var (doc, etag) = result.Value;
            var auditEntries = await _repository.GetAuditHistoryAsync(specId, cancellationToken);

            var viewModel = new DecisionSpecDetailsViewModel
            {
                SpecId = doc.SpecId,
                Version = doc.Version,
                Status = doc.Status,
                ETag = etag,
                Metadata = new DecisionSpecMetadataViewModel
                {
                    Name = doc.Metadata.Name,
                    Description = doc.Metadata.Description,
                    Tags = doc.Metadata.Tags?.ToList() ?? new List<string>()
                },
                QuestionCount = doc.Questions.Count,
                OutcomeCount = doc.Outcomes.Count,
                CreatedAt = doc.Metadata.CreatedAt,
                UpdatedAt = doc.Metadata.UpdatedAt,
                CreatedBy = doc.Metadata.CreatedBy,
                UpdatedBy = doc.Metadata.UpdatedBy,
                AuditHistory = auditEntries.Select(a => new AuditEventViewModel
                {
                    Id = a.AuditId,
                    Action = a.Action,
                    Summary = a.Summary,
                    Actor = a.Actor,
                    Source = a.Source,
                    CreatedAt = a.CreatedAt
                }).ToList()
            };

            return View(viewModel);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading details for DecisionSpec {SpecId}", specId);
            TempData["Error"] = "Failed to load DecisionSpec details. Please try again.";
            return RedirectToAction(nameof(Index));
        }
    }

    /// <summary>
    /// Handles soft delete of a DecisionSpec.
    /// </summary>
    [HttpPost("Delete/{specId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(
        string specId,
        [FromQuery] string version,
        [FromForm] string etag,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var success = await _repository.DeleteAsync(specId, version, etag, cancellationToken);
            
            if (!success)
            {
                TempData["Error"] = $"DecisionSpec '{specId}' not found.";
            }
            else
            {
                _logger.LogInformation("Soft-deleted DecisionSpec {SpecId} version {Version}", specId, version);
                TempData["Success"] = $"DecisionSpec '{specId}' has been deleted.";
            }
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("ETag mismatch"))
        {
            _logger.LogWarning("Concurrency conflict while deleting DecisionSpec {SpecId}", specId);
            TempData["Error"] = "This DecisionSpec has been modified. Please refresh and try again.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting DecisionSpec {SpecId}", specId);
            TempData["Error"] = "An error occurred while deleting. Please try again.";
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Handles restoring a soft-deleted DecisionSpec.
    /// </summary>
    [HttpPost("Restore/{specId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Restore(
        string specId,
        [FromQuery] string version,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _repository.RestoreAsync(specId, version, cancellationToken);
            
            if (result == null)
            {
                TempData["Error"] = $"DecisionSpec '{specId}' not found in archive.";
            }
            else
            {
                _logger.LogInformation("Restored DecisionSpec {SpecId} version {Version}", specId, version);
                TempData["Success"] = $"DecisionSpec '{specId}' has been restored.";
                
                return RedirectToAction(nameof(Details), new { specId });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error restoring DecisionSpec {SpecId}", specId);
            TempData["Error"] = "An error occurred while restoring. Please try again.";
        }

        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Transitions a DecisionSpec to a new lifecycle status.
    /// </summary>
    [HttpPost("TransitionStatus/{specId}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TransitionStatus(
        string specId,
        [FromForm] string newStatus,
        [FromForm] string etag,
        [FromForm] string? comment,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var current = await _repository.GetAsync(specId, null, cancellationToken);
            
            if (current == null)
            {
                TempData["Error"] = $"DecisionSpec '{specId}' not found.";
                return RedirectToAction(nameof(Index));
            }

            var (doc, currentETag) = current.Value;

            // Verify ETag
            if (currentETag != etag)
            {
                TempData["Error"] = "This DecisionSpec has been modified. Please refresh and try again.";
                return RedirectToAction(nameof(Details), new { specId });
            }

            // Update status
            doc.Status = newStatus;
            doc.Metadata.UpdatedAt = DateTimeOffset.UtcNow;
            doc.Metadata.UpdatedBy = User.Identity?.Name ?? "Admin";

            var result = await _repository.UpdateAsync(specId, doc, etag, cancellationToken);

            if (result == null)
            {
                TempData["Error"] = "Failed to update status.";
            }
            else
            {
                _logger.LogInformation("Transitioned DecisionSpec {SpecId} to status {NewStatus}", specId, newStatus);
                TempData["Success"] = $"Status updated to '{newStatus}'.";
            }
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("ETag mismatch"))
        {
            TempData["Error"] = "This DecisionSpec has been modified. Please refresh and try again.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error transitioning status for DecisionSpec {SpecId}", specId);
            TempData["Error"] = "An error occurred while updating status. Please try again.";
        }

        return RedirectToAction(nameof(Details), new { specId });
    }

    #region Helper Methods

    private static DecisionSpark.Core.Models.Spec.DecisionSpecDocument MapToDocument(DecisionSpecEditViewModel viewModel)
    {
        return new DecisionSpark.Core.Models.Spec.DecisionSpecDocument
        {
            SpecId = viewModel.SpecId,
            Version = viewModel.Version,
            Status = viewModel.Status,
            Metadata = new DecisionSpark.Core.Models.Spec.DecisionSpecMetadata
            {
                Name = viewModel.Metadata.Name,
                Description = viewModel.Metadata.Description,
                Tags = viewModel.Metadata.Tags
            },
            Questions = viewModel.Questions.Select(q => new DecisionSpark.Core.Models.Spec.Question
            {
                QuestionId = q.QuestionId,
                Type = q.Type,
                Prompt = q.Prompt,
                HelpText = q.HelpText,
                Required = q.Required,
                Options = q.Options.Select(o => new DecisionSpark.Core.Models.Spec.Option
                {
                    OptionId = o.OptionId,
                    Label = o.Label,
                    Value = o.Value,
                    NextQuestionId = o.NextQuestionId
                }).ToList(),
                Validation = q.Validation
            }).ToList(),
            Outcomes = viewModel.Outcomes.Select(o => new DecisionSpark.Core.Models.Spec.Outcome
            {
                OutcomeId = o.OutcomeId,
                SelectionRules = o.SelectionRules
            }).ToList()
        };
    }

    #endregion
}
