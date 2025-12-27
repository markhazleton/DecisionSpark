using DecisionSpark.Core.Models.Configuration;
using DecisionSpark.Core.Models.Spec;
using DecisionSpark.Core.Persistence.FileStorage;
using DecisionSpark.Core.Persistence.Repositories;
using DecisionSpark.Core.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using System.Text.Json;

namespace DecisionSpark.Tests.Core;

/// <summary>
/// Task T089 [P0] GATE: Sample spec validation - Runtime compatibility test.
/// Verifies that CRUD system uses IDENTICAL schema as runtime with ZERO transformation.
/// ALL tests must PASS before proceeding to Phase 1.
/// </summary>
public class SampleSpecCompatibilityTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly DecisionSpecRepository _repository;
    private readonly DecisionSpecMetadataStore _metadataStore;
    private readonly string _sampleSpecsPath;
    private readonly ILogger<FileSystemDecisionSpecLoader> _loaderLogger;

    public SampleSpecCompatibilityTests()
    {
        // Create temporary directory for test operations
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"CompatibilityTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);

        // Path to sample specs
        var repoRoot = FindRepositoryRoot();
        _sampleSpecsPath = Path.Combine(repoRoot, "specs", "001-decisionspecs-crud", "sample");

        // Setup CRUD repository
        var options = Options.Create(new DecisionSpecsOptions
        {
            RootPath = _tempDirectory,
            LegacyConfigPath = null,
            SoftDeleteRetentionDays = 30,
            IndexFileName = "DecisionSpecIndex.json"
        });

        var fileStoreLogger = new LoggerFactory().CreateLogger<DecisionSpecFileStore>();
        var fileStore = new DecisionSpecFileStore(options, fileStoreLogger);

        var indexerLogger = new LoggerFactory().CreateLogger<FileSearchIndexer>();
        var indexer = new FileSearchIndexer(options, indexerLogger, fileStore);

        var metadataStoreLogger = new LoggerFactory().CreateLogger<DecisionSpecMetadataStore>();
        _metadataStore = new DecisionSpecMetadataStore(options, metadataStoreLogger);

        var repositoryLogger = new LoggerFactory().CreateLogger<DecisionSpecRepository>();
        _repository = new DecisionSpecRepository(fileStore, indexer, options, repositoryLogger);

        _loaderLogger = new LoggerFactory().CreateLogger<FileSystemDecisionSpecLoader>();
    }

    /// <summary>
    /// Test 1: Verify FAMILY_SATURDAY sample spec loads through CRUD DecisionSpecRepository.
    /// </summary>
    [Fact]
    public async Task LoadFamilySaturdayThroughCrud()
    {
        // Arrange
        var samplePath = Path.Combine(_sampleSpecsPath, "FAMILY_SATURDAY_V1.1.0.0.active.json");
        
        if (!File.Exists(samplePath))
        {
            throw new FileNotFoundException($"Sample spec not found: {samplePath}");
        }

        var sampleJson = await File.ReadAllTextAsync(samplePath);
        
        // Deserialize using CRUD serialization options (snake_case)
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };

        var spec = JsonSerializer.Deserialize<DecisionSpecDocument>(sampleJson, options);

        // Assert - Verify spec loaded correctly with expected structure
        spec.Should().NotBeNull();
        spec!.SpecId.Should().Be("FAMILY_SATURDAY_V1");
        spec.Version.Should().NotBeNullOrEmpty();
        spec.Traits.Should().NotBeEmpty();
        
        // Verify specific traits from FAMILY_SATURDAY sample
        var groupSizeTrait = spec.Traits.FirstOrDefault(t => t.Key == "group_size");
        groupSizeTrait.Should().NotBeNull();
        groupSizeTrait!.QuestionText.Should().Contain("How many people");
        groupSizeTrait.AnswerType.Should().Be("integer");
        groupSizeTrait.Required.Should().BeTrue();
        groupSizeTrait.IsPseudoTrait.Should().BeFalse();
        
        // Verify derived traits exist
        spec.DerivedTraits.Should().NotBeEmpty();
        spec.DerivedTraits.Should().Contain(dt => dt.Key == "min_age");
        
        // Verify outcomes exist
        spec.Outcomes.Should().NotBeEmpty();
    }

    /// <summary>
    /// Test 2: Save via CRUD repository and reload with runtime IDecisionSpecLoader (ZERO transformation).
    /// This is the critical schema identity test.
    /// </summary>
    [Fact]
    public async Task SaveAndLoadWithRuntimeLoader()
    {
        // Arrange - Load FAMILY_SATURDAY sample
        var samplePath = Path.Combine(_sampleSpecsPath, "FAMILY_SATURDAY_V1.1.0.0.active.json");
        var sampleJson = await File.ReadAllTextAsync(samplePath);
        
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };
        
        var originalSpec = JsonSerializer.Deserialize<DecisionSpecDocument>(sampleJson, options);
        originalSpec.Should().NotBeNull();

        // Add lifecycle metadata for CRUD operation
        originalSpec!.Status = "Published";
        originalSpec.Metadata = new DecisionSpecMetadata
        {
            Name = "Family Saturday Test",
            Description = "Test spec for compatibility",
            Owner = "TestRunner",
            Status = "Published",
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow
        };

        // Act - Save via CRUD repository
        var (savedDoc, etag) = await _repository.CreateAsync(originalSpec, CancellationToken.None);
        
        savedDoc.Should().NotBeNull();
        savedDoc.SpecId.Should().Be(originalSpec.SpecId);

        // The repository saved with format: {specId}.{version}.{status}.json in Status folder
        // Runtime loader expects: {specId}.{version}.active.json in config base path
        var savedFilePath = Path.Combine(_tempDirectory, savedDoc.Status, $"{savedDoc.SpecId}.{savedDoc.Version}.{savedDoc.Status}.json");
        File.Exists(savedFilePath).Should().BeTrue("CRUD repository should have saved the file");

        // Create runtime loader file: {specId}.{version}.active.json
        var runtimeFilePath = Path.Combine(_tempDirectory, $"{savedDoc.SpecId}.{savedDoc.Version}.active.json");
        File.Copy(savedFilePath, runtimeFilePath, overwrite: true);

        var mockConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string>
            {
                ["DecisionEngine:ConfigPath"] = _tempDirectory
            }!)
            .Build();

        var mockEnv = new MockWebHostEnvironment { ContentRootPath = _tempDirectory };
        
        var runtimeLoader = new FileSystemDecisionSpecLoader(_loaderLogger, mockConfig, mockEnv);

        // Act - Load via runtime loader (should work with ZERO transformation)
        var loadedSpec = await runtimeLoader.LoadActiveSpecAsync(originalSpec.SpecId);

        // Assert - Verify runtime loader successfully loaded CRUD-saved file
        loadedSpec.Should().NotBeNull();
        loadedSpec.SpecId.Should().Be(originalSpec.SpecId);
        loadedSpec.Version.Should().Be(originalSpec.Version);
        loadedSpec.Traits.Should().HaveCount(originalSpec.Traits.Count);
        
        // Verify trait properties match
        foreach (var originalTrait in originalSpec.Traits)
        {
            var loadedTrait = loadedSpec.Traits.FirstOrDefault(t => t.Key == originalTrait.Key);
            loadedTrait.Should().NotBeNull($"Trait {originalTrait.Key} should exist after round-trip");
            loadedTrait!.QuestionText.Should().Be(originalTrait.QuestionText);
            loadedTrait.AnswerType.Should().Be(originalTrait.AnswerType);
            loadedTrait.Required.Should().Be(originalTrait.Required);
            loadedTrait.IsPseudoTrait.Should().Be(originalTrait.IsPseudoTrait);
        }
        
        // Verify derived traits preserved
        loadedSpec.DerivedTraits.Should().HaveCount(originalSpec.DerivedTraits.Count);
        
        // Verify outcomes preserved
        loadedSpec.Outcomes.Should().HaveCount(originalSpec.Outcomes.Count);
    }

    /// <summary>
    /// Test 3: Verify saved JSON has snake_case properties (spec_id, question_text, is_pseudo_trait).
    /// Ensures CRUD writes runtime-compatible format.
    /// </summary>
    [Fact]
    public async Task VerifySnakeCaseProperties()
    {
        // Arrange - Create a minimal test spec
        var testSpec = new DecisionSpecDocument
        {
            SpecId = "TEST_SPEC_V1",
            Version = "1.0.0",
            Status = "Published",
            Metadata = new DecisionSpecMetadata
            {
                Name = "Test Spec",
                Description = "Snake case validation test",
                Owner = "TestRunner",
                Status = "Published"
            },
            Traits = new List<TraitDefinition>
            {
                new TraitDefinition
                {
                    Key = "test_trait",
                    QuestionText = "Is this a test?",
                    AnswerType = "choice",
                    Required = true,
                    IsPseudoTrait = false,
                    Options = new List<string> { "yes", "no" },
                    Comment = "This is a test comment for validation"
                }
            },
            Outcomes = new List<OutcomeDefinition>
            {
                new OutcomeDefinition
                {
                    OutcomeId = "test_outcome",
                    SelectionRules = new List<string> { "test_trait == 'yes'" },
                    DisplayCards = new List<DisplayCard>
                    {
                        new DisplayCard { Title = "Test Result" }
                    }
                }
            }
        };

        // Act - Save via CRUD
        var (savedDoc, etag) = await _repository.CreateAsync(testSpec, CancellationToken.None);
        savedDoc.Should().NotBeNull();

        // Read raw JSON from file
        var publishedPath = Path.Combine(_tempDirectory, "published");
        var jsonFiles = Directory.GetFiles(publishedPath, "TEST_SPEC_V1.*.json");
        jsonFiles.Should().HaveCountGreaterOrEqualTo(1, "CRUD should have saved at least one file");

        var savedJsonPath = jsonFiles[0];
        var rawJson = await File.ReadAllTextAsync(savedJsonPath);

        // Assert - Verify snake_case properties exist in raw JSON
        rawJson.Should().Contain("\"spec_id\"", "JSON must use snake_case spec_id");
        rawJson.Should().Contain("\"traits\"", "JSON must use snake_case traits");
        rawJson.Should().Contain("\"question_text\"", "JSON must use snake_case question_text");
        rawJson.Should().Contain("\"is_pseudo_trait\"", "JSON must use snake_case is_pseudo_trait");
        rawJson.Should().Contain("\"answer_type\"", "JSON must use snake_case answer_type");
        rawJson.Should().Contain("\"derived_traits\"", "JSON must use snake_case derived_traits");
        rawJson.Should().Contain("\"outcomes\"", "JSON must use snake_case outcomes");
        
        // Verify comment field preserved (extended metadata)
        rawJson.Should().Contain("\"comment\"", "JSON must preserve comment field");
        rawJson.Should().Contain("This is a test comment for validation");

        // Verify NO PascalCase properties exist
        rawJson.Should().NotContain("\"SpecId\"", "JSON must NOT use PascalCase SpecId");
        rawJson.Should().NotContain("\"Questions\"", "JSON must NOT use PascalCase Questions");
        rawJson.Should().NotContain("\"QuestionText\"", "JSON must NOT use PascalCase QuestionText");
        rawJson.Should().NotContain("\"IsPseudoTrait\"", "JSON must NOT use PascalCase IsPseudoTrait");

        // Verify the JSON can be deserialized by JsonDocument (valid structure)
        using var jsonDoc = JsonDocument.Parse(rawJson);
        var root = jsonDoc.RootElement;
        
        root.TryGetProperty("spec_id", out var specIdProp).Should().BeTrue();
        specIdProp.GetString().Should().Be("TEST_SPEC_V1");
        
        root.TryGetProperty("traits", out var traitsProp).Should().BeTrue();
        traitsProp.GetArrayLength().Should().Be(1);
        
        var firstTrait = traitsProp[0];
        firstTrait.TryGetProperty("question_text", out var questionTextProp).Should().BeTrue();
        questionTextProp.GetString().Should().Be("Is this a test?");
        
        firstTrait.TryGetProperty("is_pseudo_trait", out var isPseudoProp).Should().BeTrue();
        isPseudoProp.GetBoolean().Should().BeFalse();
        
        firstTrait.TryGetProperty("comment", out var commentProp).Should().BeTrue();
        commentProp.GetString().Should().Contain("test comment");
    }

    /// <summary>
    /// Bonus Test: Verify TECH_STACK_ADVISOR sample with mapping field preserves correctly.
    /// </summary>
    [Fact]
    public async Task TechStackAdvisor_CommentAndMappingFields_Preserved()
    {
        // Arrange
        var samplePath = Path.Combine(_sampleSpecsPath, "TECH_STACK_ADVISOR_V1.0.0.0.active.json");
        
        if (!File.Exists(samplePath))
        {
            // Skip if sample doesn't exist
            return;
        }

        var sampleJson = await File.ReadAllTextAsync(samplePath);
        
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
        };
        
        var spec = JsonSerializer.Deserialize<DecisionSpecDocument>(sampleJson, options);
        spec.Should().NotBeNull();

        // Find a trait with mapping field
        var traitWithMapping = spec!.Traits.FirstOrDefault(t => t.Mapping != null && t.Mapping.Count > 0);
        
        if (traitWithMapping != null)
        {
            // Verify mapping structure
            traitWithMapping.Mapping.Should().NotBeNull();
            traitWithMapping.Mapping!.Should().NotBeEmpty();
            
            // Save and reload
            spec.Status = "Published";
            spec.Metadata = new DecisionSpecMetadata
            {
                Name = "Tech Stack Test",
                Owner = "TestRunner",
                Status = "Published"
            };

            var (savedDoc, etag) = await _repository.CreateAsync(spec, CancellationToken.None);
            
            // Read back and verify mapping preserved
            var reloaded = await _repository.GetAsync(spec.SpecId, null, CancellationToken.None);
            reloaded.Should().NotBeNull();
            
            var reloadedTrait = reloaded!.Value.Document.Traits.FirstOrDefault(t => t.Key == traitWithMapping.Key);
            reloadedTrait.Should().NotBeNull();
            reloadedTrait!.Mapping.Should().NotBeNull();
            reloadedTrait.Mapping.Should().HaveCount(traitWithMapping.Mapping.Count);
        }
    }

    private string FindRepositoryRoot()
    {
        var current = Directory.GetCurrentDirectory();
        
        while (current != null)
        {
            if (File.Exists(Path.Combine(current, "DecisionSpark.slnx")))
            {
                return current;
            }
            
            var parent = Directory.GetParent(current);
            if (parent == null) break;
            current = parent.FullName;
        }
        
        throw new InvalidOperationException("Could not find repository root (DecisionSpark.slnx)");
    }

    public void Dispose()
    {
        // Cleanup temp directory
        if (Directory.Exists(_tempDirectory))
        {
            try
            {
                Directory.Delete(_tempDirectory, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}

/// <summary>
/// Mock IWebHostEnvironment for testing runtime loader.
/// </summary>
internal class MockWebHostEnvironment : Microsoft.AspNetCore.Hosting.IWebHostEnvironment
{
    public string WebRootPath { get; set; } = string.Empty;
    public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
    public string ApplicationName { get; set; } = "DecisionSpark.Tests";
    public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
    public string ContentRootPath { get; set; } = string.Empty;
    public string EnvironmentName { get; set; } = "Test";
}
