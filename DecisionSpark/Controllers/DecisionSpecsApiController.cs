using DecisionSpark.Middleware;
using DecisionSpark.Models.Api.DecisionSpecs;
using DecisionSpark.Core.Models.Spec;
using DecisionSpark.Core.Persistence.Repositories;
using DecisionSpark.Core.Services;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace DecisionSpark.Controllers;

/// <summary>
/// API controller for DecisionSpec CRUD operations.
/// Includes comprehensive structured logging and metrics for observability.
/// </summary>
[ApiController]
[Route("api/decisionspecs")]
public class DecisionSpecsApiController : ControllerBase
{
    private readonly IDecisionSpecRepository _repository;
    private readonly TraitPatchService _traitPatchService;
    private readonly DecisionSpecDraftService? _draftService;
    private readonly ILogger<DecisionSpecsApiController> _logger;

    public DecisionSpecsApiController(
        IDecisionSpecRepository repository,
        TraitPatchService traitPatchService,
        ILogger<DecisionSpecsApiController> logger,
        DecisionSpecDraftService? draftService = null)
    {
        _repository = repository;
        _traitPatchService = traitPatchService;
        _draftService = draftService;
        _logger = logger;
    }

    /// <summary>
    /// Lists DecisionSpecs with optional filters.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(DecisionSpecListResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<DecisionSpecListResponse>> List(
        [FromQuery] string? status = null,
        [FromQuery] string? owner = null,
        [FromQuery] string? q = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("Listing DecisionSpecs with filters: Status={Status}, Owner={Owner}, Query={Query}, Page={Page}, PageSize={PageSize}",
            status, owner, q, page, pageSize);

        var summaries = await _repository.ListAsync(status, owner, q, cancellationToken);
        var items = summaries
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(MapToSummaryDto)
            .ToList();

        sw.Stop();
        _logger.LogInformation("Listed {Count} DecisionSpecs (total: {Total}) in {ElapsedMs}ms",
            items.Count, summaries.Count(), sw.ElapsedMilliseconds);

        return Ok(new DecisionSpecListResponse
        {
            Items = items,
            Total = summaries.Count(),
            Page = page,
            PageSize = pageSize
        });
    }

    /// <summary>
    /// Creates a new DecisionSpec.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(DecisionSpecDocumentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(Microsoft.AspNetCore.Mvc.ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DecisionSpecDocumentDto>> Create(
        [FromBody] DecisionSpecCreateRequest request,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("Creating DecisionSpec {SpecId} v{Version}", request.SpecId, request.Version);

        if (!ModelState.IsValid)
        {
            _logger.LogWarning("Validation failed for creating DecisionSpec {SpecId}: {Errors}",
                request.SpecId, string.Join("; ", ModelState.Values.SelectMany(v => v.Errors.Select(e => e.ErrorMessage))));
            return BadRequest(ModelState);
        }

        var doc = MapToDocument(request);
        var (created, etag) = await _repository.CreateAsync(doc, cancellationToken);

        sw.Stop();
        _logger.LogInformation("Created DecisionSpec {SpecId} v{Version} with {TraitCount} traits and {OutcomeCount} outcomes in {ElapsedMs}ms - SC-004: audit trail",
            created.SpecId, created.Version, created.Traits.Count, created.Outcomes.Count, sw.ElapsedMilliseconds);

        Response.Headers.Append("ETag", etag);
        
        var dto = MapToDocumentDto(created);
        return CreatedAtAction(nameof(Get), new { specId = created.SpecId }, dto);
    }

    /// <summary>
    /// Retrieves a DecisionSpec by ID.
    /// </summary>
    [HttpGet("{specId}")]
    [ProducesResponseType(typeof(DecisionSpecDocumentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DecisionSpecDocumentDto>> Get(
        string specId,
        [FromQuery] string? version = null,
        CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("Retrieving DecisionSpec {SpecId} v{Version}", specId, version ?? "latest");

        var result = await _repository.GetAsync(specId, version, cancellationToken);
        
        if (result == null)
        {
            _logger.LogWarning("DecisionSpec {SpecId} v{Version} not found", specId, version ?? "latest");
            return NotFound();
        }

        var (doc, etag) = result.Value;
        Response.Headers.Append("ETag", etag);

        sw.Stop();
        _logger.LogInformation("Retrieved DecisionSpec {SpecId} v{Version} in {ElapsedMs}ms",
            specId, version ?? "latest", sw.ElapsedMilliseconds);

        return Ok(MapToDocumentDto(doc));
    }

    /// <summary>
    /// Replaces an entire DecisionSpec document.
    /// </summary>
    [HttpPut("{specId}")]
    [ProducesResponseType(typeof(DecisionSpecDocumentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Microsoft.AspNetCore.Mvc.ValidationProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DecisionSpecDocumentDto>> Update(
        string specId,
        [FromBody] DecisionSpecDocumentDto request,
        [FromHeader(Name = "If-Match")] string ifMatch,
        CancellationToken cancellationToken = default)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            return BadRequest(new Microsoft.AspNetCore.Mvc.ValidationProblemDetails(
                new Dictionary<string, string[]>
                {
                    ["If-Match"] = new[] { "If-Match header is required for updates" }
                })
            {
                Title = "If-Match header is required",
                Status = 400
            });
        }

        try
        {
            var doc = MapToDocument(request);
            var result = await _repository.UpdateAsync(specId, doc, ifMatch, cancellationToken);

            if (result == null)
            {
                return NotFound();
            }

            var (updated, etag) = result.Value;
            Response.Headers.Append("ETag", etag);

            return Ok(MapToDocumentDto(updated));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("ETag mismatch"))
        {
            _logger.LogWarning(ex, "Concurrent edit collision detected for {SpecId} - SC-005: concurrent edit collision detection", specId);
            return Conflict(new Microsoft.AspNetCore.Mvc.ValidationProblemDetails(
                new Dictionary<string, string[]>
                {
                    ["ETag"] = new[] { "The DecisionSpec has been modified by another user. Please reload and try again." }
                })
            {
                Title = "Concurrency conflict",
                Status = 409
            });
        }
    }

    /// <summary>
    /// Soft-deletes a DecisionSpec.
    /// </summary>
    [HttpDelete("{specId}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Microsoft.AspNetCore.Mvc.ValidationProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(
        string specId,
        [FromQuery] string version,
        [FromHeader(Name = "If-Match")] string ifMatch,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            return BadRequest(new Microsoft.AspNetCore.Mvc.ValidationProblemDetails(
                new Dictionary<string, string[]>
                {
                    ["If-Match"] = new[] { "If-Match header is required for deletions" }
                })
            {
                Title = "If-Match header is required",
                Status = 400
            });
        }

        try
        {
            var success = await _repository.DeleteAsync(specId, version, ifMatch, cancellationToken);
            
            if (!success)
            {
                return NotFound();
            }

            return NoContent();
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("ETag mismatch"))
        {
            return Conflict(new Microsoft.AspNetCore.Mvc.ValidationProblemDetails(
                new Dictionary<string, string[]>
                {
                    ["ETag"] = new[] { "The DecisionSpec has been modified. Please reload and try again." }
                })
            {
                Title = "Concurrency conflict",
                Status = 409
            });
        }
    }

    /// <summary>
    /// Retrieves the full DecisionSpec document optimized for runtime consumption.
    /// </summary>
    [HttpGet("{specId}/full")]
    [ProducesResponseType(typeof(DecisionSpecDocumentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetFullDocument(
        string specId,
        [FromQuery] string? version = null,
        CancellationToken cancellationToken = default)
    {
        var json = await _repository.GetFullDocumentJsonAsync(specId, version, cancellationToken);
        
        if (json == null)
        {
            return NotFound();
        }

        // Get ETag for the document
        var result = await _repository.GetAsync(specId, version, cancellationToken);
        if (result != null)
        {
            Response.Headers.Append("ETag", result.Value.ETag);
        }

        return Content(json, "application/json");
    }

    /// <summary>
    /// Restores a soft-deleted DecisionSpec.
    /// </summary>
    [HttpPost("{specId}/restore")]
    [ProducesResponseType(typeof(DecisionSpecDocumentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DecisionSpecDocumentDto>> Restore(
        string specId,
        [FromQuery] string version,
        CancellationToken cancellationToken = default)
    {
        var result = await _repository.RestoreAsync(specId, version, cancellationToken);

        if (result == null)
        {
            return NotFound();
        }

        var (doc, etag) = result.Value;
        Response.Headers.Append("ETag", etag);

        return Ok(MapToDocumentDto(doc));
    }

    /// <summary>
    /// Retrieves audit history for a DecisionSpec.
    /// </summary>
    [HttpGet("{specId}/audit")]
    [ProducesResponseType(typeof(AuditLogResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<AuditLogResponse>> GetAuditHistory(
        string specId,
        CancellationToken cancellationToken = default)
    {
        var entries = await _repository.GetAuditHistoryAsync(specId, cancellationToken);

        return Ok(new AuditLogResponse
        {
            SpecId = specId,
            Events = entries.Select(e => new AuditEventDto
            {
                Id = e.AuditId,
                Action = e.Action,
                Summary = e.Summary,
                Actor = e.Actor,
                Source = e.Source,
                CreatedAt = e.CreatedAt
            }).ToList()
        });
    }

    /// <summary>
    /// Patches a single question within a DecisionSpec.
    /// </summary>
    [HttpPatch("{specId}/traits/{traitKey}")]
    [ProducesResponseType(typeof(DecisionSpecDocumentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Microsoft.AspNetCore.Mvc.ValidationProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DecisionSpecDocumentDto>> PatchTrait(
        string specId,
        string traitKey,
        [FromBody] TraitPatchRequest request,
        [FromHeader(Name = "If-Match")] string ifMatch,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            return BadRequest(new Microsoft.AspNetCore.Mvc.ValidationProblemDetails(
                new Dictionary<string, string[]>
                {
                    ["If-Match"] = new[] { "If-Match header is required for patches" }
                })
            {
                Title = "If-Match header is required",
                Status = 400
            });
        }

        try
        {
            var result = await _traitPatchService.PatchTraitAsync(
                specId,
                traitKey,
                request.QuestionText,
                request.ParseHint,
                request.Options?.Select(o => o.Value).ToList(),
                request.Bounds,
                request.Comment,
                ifMatch,
                User.Identity?.Name ?? "API",
                cancellationToken);

            if (result == null)
            {
                return NotFound();
            }

            var (doc, etag) = result.Value;
            Response.Headers.Append("ETag", etag);

            return Ok(MapToDocumentDto(doc));
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new Microsoft.AspNetCore.Mvc.ValidationProblemDetails(
                new Dictionary<string, string[]>
                {
                    ["traitKey"] = new[] { $"Trait {traitKey} not found in spec {specId}" }
                })
            {
                Title = "Trait not found",
                Status = 404
            });
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("ETag mismatch"))
        {
            return Conflict(new Microsoft.AspNetCore.Mvc.ValidationProblemDetails(
                new Dictionary<string, string[]>
                {
                    ["ETag"] = new[] { "The DecisionSpec has been modified. Please reload and try again." }
                })
            {
                Title = "Concurrency conflict",
                Status = 409
            });
        }
    }
    /// <summary>
    /// Transitions a DecisionSpec to a new lifecycle status.
    /// </summary>
    [HttpPost("{specId}/status")]
    [ProducesResponseType(typeof(DecisionSpecDocumentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(Microsoft.AspNetCore.Mvc.ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DecisionSpecDocumentDto>> TransitionStatus(
        string specId,
        [FromBody] StatusTransitionRequest request,
        [FromHeader(Name = "If-Match")] string ifMatch,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ifMatch))
        {
            return BadRequest(new Microsoft.AspNetCore.Mvc.ValidationProblemDetails(
                new Dictionary<string, string[]>
                {
                    ["If-Match"] = new[] { "If-Match header is required for status transitions" }
                })
            {
                Title = "If-Match header is required",
                Status = 400
            });
        }

        try
        {
            // Get current spec
            var current = await _repository.GetAsync(specId, null, cancellationToken);
            if (current == null)
            {
                return NotFound();
            }

            var (doc, currentETag) = current.Value;

            // Verify ETag
            if (currentETag != ifMatch)
            {
                return Conflict(new Microsoft.AspNetCore.Mvc.ValidationProblemDetails(
                    new Dictionary<string, string[]>
                    {
                        ["ETag"] = new[] { "The DecisionSpec has been modified. Please reload and try again." }
                    })
                {
                    Title = "Concurrency conflict",
                    Status = 409
                });
            }

            // Validate transition
            if (!IsValidTransition(doc.Status, request.NewStatus))
            {
                return BadRequest(new Microsoft.AspNetCore.Mvc.ValidationProblemDetails(
                    new Dictionary<string, string[]>
                    {
                        ["newStatus"] = new[] { $"Cannot transition from {doc.Status} to {request.NewStatus}" }
                    })
                {
                    Title = "Invalid status transition",
                    Status = 400
                });
            }

            // Update status
            doc.Status = request.NewStatus;
            doc.Metadata.UpdatedAt = DateTimeOffset.UtcNow;
            doc.Metadata.UpdatedBy = User.Identity?.Name ?? "API";

            var result = await _repository.UpdateAsync(specId, doc, ifMatch, cancellationToken);

            if (result == null)
            {
                return NotFound();
            }

            var (updated, etag) = result.Value;
            Response.Headers.Append("ETag", etag);

            return Ok(MapToDocumentDto(updated));
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("ETag mismatch"))
        {
            return Conflict(new Microsoft.AspNetCore.Mvc.ValidationProblemDetails(
                new Dictionary<string, string[]>
                {
                    ["ETag"] = new[] { "The DecisionSpec has been modified. Please reload and try again." }
                })
            {
                Title = "Concurrency conflict",
                Status = 409
            });
        }
    }

    /// <summary>
    /// Validates lifecycle state transitions.
    /// </summary>
    private static bool IsValidTransition(string currentStatus, string newStatus)
    {
        return (currentStatus, newStatus) switch
        {
            ("Draft", "InReview") => true,
            ("Draft", "Retired") => true,
            ("InReview", "Draft") => true,
            ("InReview", "Published") => true,
            ("Published", "InReview") => true,
            ("Published", "Retired") => true,
            _ => false
        };
    }
    // Helper methods for mapping
    private static DecisionSpecSummaryDto MapToSummaryDto(DecisionSpecSummary summary)
    {
        return new DecisionSpecSummaryDto
        {
            SpecId = summary.SpecId,
            Name = summary.Name,
            Status = summary.Status,
            Owner = summary.Owner,
            Version = summary.Version,
            UpdatedAt = summary.UpdatedAt,
            TraitCount = summary.TraitCount,
            HasUnverifiedDraft = summary.HasUnverifiedDraft
        };
    }

    private static DecisionSpecDocument MapToDocument(DecisionSpecCreateRequest request)
    {
        return new DecisionSpecDocument
        {
            SpecId = request.SpecId,
            Version = request.Version,
            Status = request.Status,
            Metadata = new DecisionSpecMetadata
            {
                Name = request.Metadata.Name,
                Description = request.Metadata.Description,
                Tags = request.Metadata.Tags
            },
            Traits = request.Traits.Select(t => new TraitDefinition
            {
                Key = t.Key,
                QuestionText = t.QuestionText,
                AnswerType = t.AnswerType,
                ParseHint = t.ParseHint ?? string.Empty,
                Required = t.Required,
                IsPseudoTrait = t.IsPseudoTrait,
                AllowMultiple = t.AllowMultiple,
                DependsOn = t.DependsOn != null ? new List<string> { t.DependsOn } : new List<string>(),
                Bounds = t.Bounds != null ? new TraitBounds
                {
                    Min = t.Bounds.TryGetValue("min", out var min) ? Convert.ToInt32(min) : 0,
                    Max = t.Bounds.TryGetValue("max", out var max) ? Convert.ToInt32(max) : int.MaxValue
                } : null,
                Options = t.Options?.Select(o => o.Value).ToList(),
                Mapping = t.Mapping,
                Comment = t.Comment
            }).ToList(),
            Outcomes = request.Outcomes.Select(o => new OutcomeDefinition
            {
                OutcomeId = o.OutcomeId,
                SelectionRules = o.SelectionRules,
                DisplayCards = o.DisplayCards.Select(dc => new DisplayCard
                {
                    Title = dc.ToString() ?? ""
                }).ToList()
            }).ToList()
        };
    }

    private static DecisionSpecDocument MapToDocument(DecisionSpecDocumentDto dto)
    {
        return new DecisionSpecDocument
        {
            SpecId = dto.SpecId,
            Version = dto.Version,
            Status = dto.Status,
            Metadata = new DecisionSpecMetadata
            {
                Name = dto.Metadata.Name,
                Description = dto.Metadata.Description,
                Tags = dto.Metadata.Tags
            },
            Traits = dto.Traits.Select(t => new TraitDefinition
            {
                Key = t.Key,
                QuestionText = t.QuestionText,
                AnswerType = t.AnswerType,
                ParseHint = t.ParseHint ?? string.Empty,
                Required = t.Required,
                IsPseudoTrait = t.IsPseudoTrait,
                AllowMultiple = t.AllowMultiple,
                DependsOn = t.DependsOn != null ? new List<string> { t.DependsOn } : new List<string>(),
                Bounds = t.Bounds != null ? new TraitBounds
                {
                    Min = t.Bounds.TryGetValue("min", out var min) ? Convert.ToInt32(min) : 0,
                    Max = t.Bounds.TryGetValue("max", out var max) ? Convert.ToInt32(max) : int.MaxValue
                } : null,
                Options = t.Options?.Select(o => o.Value).ToList(),
                Mapping = t.Mapping,
                Comment = t.Comment
            }).ToList(),
            Outcomes = dto.Outcomes.Select(o => new OutcomeDefinition
            {
                OutcomeId = o.OutcomeId,
                SelectionRules = o.SelectionRules,
                DisplayCards = o.DisplayCards.Select(dc => new DisplayCard
                {
                    Title = dc.ToString() ?? ""
                }).ToList()
            }).ToList()
        };
    }

    private static DecisionSpecDocumentDto MapToDocumentDto(DecisionSpecDocument doc)
    {
        return new DecisionSpecDocumentDto
        {
            SpecId = doc.SpecId,
            Version = doc.Version,
            Status = doc.Status,
            Metadata = new DecisionSpecMetadataDto
            {
                Name = doc.Metadata?.Name ?? string.Empty,
                Description = doc.Metadata?.Description ?? string.Empty,
                Tags = doc.Metadata?.Tags ?? new List<string>()
            },
            Traits = doc.Traits.Select(t => new TraitDto
            {
                Key = t.Key,
                QuestionText = t.QuestionText,
                AnswerType = t.AnswerType,
                ParseHint = t.ParseHint,
                Required = t.Required,
                IsPseudoTrait = t.IsPseudoTrait,
                AllowMultiple = t.AllowMultiple ?? false,
                DependsOn = t.DependsOn.FirstOrDefault(),
                Bounds = t.Bounds != null ? new Dictionary<string, object>
                {
                    ["min"] = t.Bounds.Min,
                    ["max"] = t.Bounds.Max
                } : null,
                Options = t.Options?.Select(o => new OptionDto
                {
                    Key = o,
                    Label = o,
                    Value = o
                }).ToList() ?? new List<OptionDto>(),
                Mapping = t.Mapping,
                Comment = t.Comment
            }).ToList(),
            Outcomes = doc.Outcomes.Select(o => new OutcomeDto
            {
                OutcomeId = o.OutcomeId,
                SelectionRules = o.SelectionRules,
                DisplayCards = o.DisplayCards.Select(dc => (object)dc).ToList()
            }).ToList()
        };
    }

    #region LLM Draft Endpoints

    /// <summary>
    /// Generates a DecisionSpec draft from a natural language instruction using LLM.
    /// </summary>
    [HttpPost("llm-drafts")]
    [ProducesResponseType(typeof(LlmDraftResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(Microsoft.AspNetCore.Mvc.ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<LlmDraftResponse>> GenerateDraft(
        [FromBody] LlmDraftRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_draftService == null)
        {
            _logger.LogWarning("LLM draft service not configured");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, 
                new { error = "LLM draft service is not available" });
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        if (string.IsNullOrWhiteSpace(request.Instruction))
        {
            ModelState.AddModelError(nameof(request.Instruction), "Instruction is required");
            return BadRequest(ModelState);
        }

        if (request.Instruction.Length > 2000)
        {
            ModelState.AddModelError(nameof(request.Instruction), "Instruction must be 2000 characters or less");
            return BadRequest(ModelState);
        }

        try
        {
            var (draft, draftId) = await _draftService.GenerateDraftAsync(request.Instruction, cancellationToken);

            _logger.LogInformation("Generated LLM draft {DraftId} from instruction", draftId);

            var response = new LlmDraftResponse
            {
                DraftId = draftId,
                Spec = MapToDocumentDto(draft),
                Status = "Ready",
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(7)
            };

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to generate LLM draft");
            return StatusCode(StatusCodes.Status503ServiceUnavailable, 
                new { error = ex.Message, details = "The LLM service may be unavailable or rate-limited. Please try again later." });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error generating LLM draft");
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { error = "An unexpected error occurred while generating the draft" });
        }
    }

    /// <summary>
    /// Retrieves a previously generated draft by ID.
    /// </summary>
    [HttpGet("llm-drafts/{draftId}")]
    [ProducesResponseType(typeof(DecisionSpecDocumentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<DecisionSpecDocumentDto>> GetDraft(
        string draftId,
        CancellationToken cancellationToken = default)
    {
        if (_draftService == null)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, 
                new { error = "LLM draft service is not available" });
        }

        try
        {
            var draft = await _draftService.GetDraftAsync(draftId, cancellationToken);

            if (draft == null)
            {
                return NotFound(new { error = $"Draft '{draftId}' not found" });
            }

            var dto = MapToDocumentDto(draft);
            return Ok(dto);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving draft {DraftId}", draftId);
            return StatusCode(StatusCodes.Status500InternalServerError, 
                new { error = "An error occurred while retrieving the draft" });
        }
    }

    #endregion
}
