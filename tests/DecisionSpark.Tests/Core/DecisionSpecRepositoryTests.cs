using DecisionSpark.Core.Models.Spec;
using DecisionSpark.Core.Persistence.FileStorage;
using DecisionSpark.Core.Persistence.Repositories;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;
using DecisionSpark.Core.Models.Configuration;
using Moq;

namespace DecisionSpark.Tests.Core;

/// <summary>
/// Tests for DecisionSpecRepository covering JSON persistence, audit append-only logs, and soft-delete archival folders.
/// Task T019: Expand repository tests to validate JSON persistence, audit append-only logs, and soft-delete archival folders.
/// </summary>
public class DecisionSpecRepositoryTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly DecisionSpecRepository _repository;
    private readonly DecisionSpecFileStore _fileStore;
    private readonly FileSearchIndexer _indexer;

    public DecisionSpecRepositoryTests()
    {
        // Create a temporary directory for test files
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"DecisionSpecTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);

        // Setup options
        var options = Options.Create(new DecisionSpecsOptions
        {
            RootPath = _tempDirectory,
            LegacyConfigPath = null, // No legacy path for tests
            SoftDeleteRetentionDays = 30,
            IndexFileName = "DecisionSpecIndex.json"
        });

        // Create dependencies
        var fileStoreLogger = new LoggerFactory().CreateLogger<DecisionSpecFileStore>();
        _fileStore = new DecisionSpecFileStore(options, fileStoreLogger);

        var indexerLogger = new LoggerFactory().CreateLogger<FileSearchIndexer>();
        _indexer = new FileSearchIndexer(options, indexerLogger, _fileStore);

        var repositoryLogger = new LoggerFactory().CreateLogger<DecisionSpecRepository>();
        _repository = new DecisionSpecRepository(_fileStore, _indexer, options, repositoryLogger);
    }

    public void Dispose()
    {
        // Clean up temp directory
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    #region Create Tests

    [Fact]
    public async Task CreateAsync_SavesSpecToFile_AndReturnsETag()
    {
        // Arrange
        var spec = CreateSampleSpec("test-spec-1", "1.0.0", "Draft");

        // Act
        var (createdDoc, etag) = await _repository.CreateAsync(spec);

        // Assert
        createdDoc.Should().NotBeNull();
        createdDoc.SpecId.Should().Be("test-spec-1");
        etag.Should().NotBeNullOrEmpty();

        // Verify file was created
        var expectedPath = Path.Combine(_tempDirectory, "draft", "test-spec-1.1.0.0.Draft.json");
        File.Exists(expectedPath).Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_CreatesAuditLog()
    {
        // Arrange
        var spec = CreateSampleSpec("test-spec-2", "1.0.0", "Draft");

        // Act
        await _repository.CreateAsync(spec);

        // Assert - Verify audit log file exists
        var auditPath = Path.Combine(_tempDirectory, "draft", "test-spec-2.audit.jsonl");
        File.Exists(auditPath).Should().BeTrue();

        // Read and verify audit entry
        var auditLines = await File.ReadAllLinesAsync(auditPath);
        auditLines.Should().NotBeEmpty();
        auditLines.First().Should().Contain("Created");
    }

    [Fact]
    public async Task CreateAsync_UpdatesSearchIndex()
    {
        // Arrange
        var spec = CreateSampleSpec("test-spec-3", "1.0.0", "Draft");

        // Act
        await _repository.CreateAsync(spec);

        // Assert - Query index and verify entry exists
        var indexResults = await _indexer.QueryAsync();
        indexResults.Should().ContainSingle(e => e.SpecId == "test-spec-3");
    }

    #endregion

    #region Get Tests

    [Fact]
    public async Task GetAsync_ReturnsSpec_WhenExists()
    {
        // Arrange
        var spec = CreateSampleSpec("test-spec-4", "1.0.0", "Draft");
        await _repository.CreateAsync(spec);

        // Act
        var result = await _repository.GetAsync("test-spec-4", "1.0.0");

        // Assert
        result.Should().NotBeNull();
        result.Value.Document.SpecId.Should().Be("test-spec-4");
        result.Value.Document.Version.Should().Be("1.0.0");
        result.Value.ETag.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task GetAsync_ReturnsLatestVersion_WhenVersionNotSpecified()
    {
        // Arrange
        var spec = CreateSampleSpec("test-spec-5", "2.1.3", "Published");
        await _repository.CreateAsync(spec);

        // Act
        var result = await _repository.GetAsync("test-spec-5");

        // Assert
        result.Should().NotBeNull();
        result.Value.Document.Version.Should().Be("2.1.3");
    }

    [Fact]
    public async Task GetAsync_ReturnsNull_WhenSpecDoesNotExist()
    {
        // Act
        var result = await _repository.GetAsync("nonexistent-spec");

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Update Tests

    [Fact]
    public async Task UpdateAsync_UpdatesSpec_WhenETagMatches()
    {
        // Arrange
        var spec = CreateSampleSpec("test-spec-6", "1.0.0", "Draft");
        var (created, etag) = await _repository.CreateAsync(spec);

        // Modify the spec
        created.Metadata.Name = "Updated Name";
        created.Metadata.Description = "Updated Description";

        // Act
        var result = await _repository.UpdateAsync("test-spec-6", created, etag);

        // Assert
        result.Should().NotBeNull();
        result.Value.Document.Metadata.Name.Should().Be("Updated Name");
        result.Value.Document.Metadata.Description.Should().Be("Updated Description");
        result.Value.ETag.Should().NotBe(etag); // ETag should change after update
    }

    [Fact]
    public async Task UpdateAsync_ThrowsException_WhenETagMismatch()
    {
        // Arrange
        var spec = CreateSampleSpec("test-spec-7", "1.0.0", "Draft");
        var (created, _) = await _repository.CreateAsync(spec);

        // Act & Assert
        var act = async () => await _repository.UpdateAsync("test-spec-7", created, "wrong-etag");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ETag mismatch*");
    }

    [Fact]
    public async Task UpdateAsync_CreatesAuditEntry()
    {
        // Arrange
        var spec = CreateSampleSpec("test-spec-8", "1.0.0", "Draft");
        var (created, etag) = await _repository.CreateAsync(spec);
        created.Metadata.Name = "Updated Name";

        // Act
        await _repository.UpdateAsync("test-spec-8", created, etag);

        // Assert - Verify audit log contains update entry
        var auditPath = Path.Combine(_tempDirectory, "draft", "test-spec-8.audit.jsonl");
        var auditLines = await File.ReadAllLinesAsync(auditPath);
        auditLines.Should().HaveCountGreaterThan(1);
        auditLines.Last().Should().Contain("Updated");
    }

    #endregion

    #region Delete Tests

    [Fact]
    public async Task DeleteAsync_MovesSpecToArchive_AndReturnsTrue()
    {
        // Arrange
        var spec = CreateSampleSpec("test-spec-9", "1.0.0", "Draft");
        var (_, etag) = await _repository.CreateAsync(spec);

        var originalPath = Path.Combine(_tempDirectory, "draft", "test-spec-9.1.0.0.Draft.json");
        File.Exists(originalPath).Should().BeTrue();

        // Act
        var result = await _repository.DeleteAsync("test-spec-9", "1.0.0", etag);

        // Assert
        result.Should().BeTrue();
        
        // Original file should be moved to archive
        File.Exists(originalPath).Should().BeFalse();
        
        // Archive directory should exist and contain the file
        var archivePath = Path.Combine(_tempDirectory, "archive");
        Directory.Exists(archivePath).Should().BeTrue();
        
        var archivedFiles = Directory.GetFiles(archivePath, "test-spec-9*");
        archivedFiles.Should().NotBeEmpty();
    }

    [Fact]
    public async Task DeleteAsync_ReturnsFalse_WhenSpecDoesNotExist()
    {
        // Act
        var result = await _repository.DeleteAsync("nonexistent", "1.0.0", "any-etag");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_ThrowsException_WhenETagMismatch()
    {
        // Arrange
        var spec = CreateSampleSpec("test-spec-10", "1.0.0", "Draft");
        await _repository.CreateAsync(spec);

        // Act & Assert
        var act = async () => await _repository.DeleteAsync("test-spec-10", "1.0.0", "wrong-etag");
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*ETag mismatch*");
    }

    [Fact]
    public async Task DeleteAsync_CreatesAuditEntry()
    {
        // Arrange
        var spec = CreateSampleSpec("test-spec-11", "1.0.0", "Draft");
        var (_, etag) = await _repository.CreateAsync(spec);

        // Act
        await _repository.DeleteAsync("test-spec-11", "1.0.0", etag);

        // Assert - Verify audit log in archive contains delete entry
        var archivePath = Path.Combine(_tempDirectory, "archive");
        var auditFiles = Directory.GetFiles(archivePath, "test-spec-11.audit.jsonl.*");
        auditFiles.Should().NotBeEmpty();

        var auditContent = await File.ReadAllTextAsync(auditFiles.First());
        auditContent.Should().Contain("Deleted");
    }

    #endregion

    #region Restore Tests

    [Fact]
    public async Task RestoreAsync_RestoresDeletedSpec_FromArchive()
    {
        // Arrange
        var spec = CreateSampleSpec("test-spec-12", "1.0.0", "Draft");
        var (_, etag) = await _repository.CreateAsync(spec);
        await _repository.DeleteAsync("test-spec-12", "1.0.0", etag);

        // Act
        var result = await _repository.RestoreAsync("test-spec-12", "1.0.0");

        // Assert
        result.Should().NotBeNull();
        result.Value.Document.SpecId.Should().Be("test-spec-12");

        // Verify file is back in original location
        var restoredPath = Path.Combine(_tempDirectory, "draft", "test-spec-12.1.0.0.Draft.json");
        File.Exists(restoredPath).Should().BeTrue();
    }

    [Fact]
    public async Task RestoreAsync_ReturnsNull_WhenSpecNotInArchive()
    {
        // Act
        var result = await _repository.RestoreAsync("nonexistent", "1.0.0");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task RestoreAsync_CreatesAuditEntry()
    {
        // Arrange
        var spec = CreateSampleSpec("test-spec-13", "1.0.0", "Draft");
        var (_, etag) = await _repository.CreateAsync(spec);
        await _repository.DeleteAsync("test-spec-13", "1.0.0", etag);

        // Act
        await _repository.RestoreAsync("test-spec-13", "1.0.0");

        // Assert - Verify audit log contains restore entry
        var auditPath = Path.Combine(_tempDirectory, "draft", "test-spec-13.audit.jsonl");
        File.Exists(auditPath).Should().BeTrue();
        
        var auditContent = await File.ReadAllTextAsync(auditPath);
        auditContent.Should().Contain("Restored");
    }

    #endregion

    #region List Tests

    [Fact]
    public async Task ListAsync_ReturnsAllSpecs_WhenNoFilters()
    {
        // Arrange
        await _repository.CreateAsync(CreateSampleSpec("spec-a", "1.0.0", "Draft"));
        await _repository.CreateAsync(CreateSampleSpec("spec-b", "1.0.0", "Published"));
        await _repository.CreateAsync(CreateSampleSpec("spec-c", "1.0.0", "InReview"));

        // Act
        var results = await _repository.ListAsync();

        // Assert
        results.Should().HaveCountGreaterOrEqualTo(3);
    }

    [Fact]
    public async Task ListAsync_FiltersByStatus()
    {
        // Arrange
        await _repository.CreateAsync(CreateSampleSpec("spec-d", "1.0.0", "Draft"));
        await _repository.CreateAsync(CreateSampleSpec("spec-e", "1.0.0", "Published"));

        // Act
        var results = await _repository.ListAsync(status: "Published");

        // Assert
        results.Should().Contain(s => s.SpecId == "spec-e");
        results.Should().NotContain(s => s.SpecId == "spec-d");
    }

    [Fact]
    public async Task ListAsync_FiltersByOwner()
    {
        // Arrange
        var spec1 = CreateSampleSpec("spec-f", "1.0.0", "Draft");
        spec1.Metadata.Owner = "alice";
        await _repository.CreateAsync(spec1);

        var spec2 = CreateSampleSpec("spec-g", "1.0.0", "Draft");
        spec2.Metadata.Owner = "bob";
        await _repository.CreateAsync(spec2);

        // Act
        var results = await _repository.ListAsync(owner: "alice");

        // Assert
        results.Should().Contain(s => s.SpecId == "spec-f");
        results.Should().NotContain(s => s.SpecId == "spec-g");
    }

    [Fact]
    public async Task ListAsync_FiltersBySearchTerm()
    {
        // Arrange
        var spec1 = CreateSampleSpec("search-test-1", "1.0.0", "Draft");
        spec1.Metadata.Name = "Customer Onboarding Flow";
        await _repository.CreateAsync(spec1);

        var spec2 = CreateSampleSpec("search-test-2", "1.0.0", "Draft");
        spec2.Metadata.Name = "Product Selection Wizard";
        await _repository.CreateAsync(spec2);

        // Act
        var results = await _repository.ListAsync(searchTerm: "Customer");

        // Assert
        results.Should().Contain(s => s.SpecId == "search-test-1");
        results.Should().NotContain(s => s.SpecId == "search-test-2");
    }

    #endregion

    #region Full Document Tests

    [Fact]
    public async Task GetFullDocumentJsonAsync_ReturnsRawJson()
    {
        // Arrange
        var spec = CreateSampleSpec("test-spec-14", "1.0.0", "Published");
        await _repository.CreateAsync(spec);

        // Act
        var json = await _repository.GetFullDocumentJsonAsync("test-spec-14", "1.0.0");

        // Assert
        json.Should().NotBeNullOrEmpty();
        json.Should().Contain("test-spec-14");
        json.Should().Contain("Published");
    }

    [Fact]
    public async Task GetFullDocumentJsonAsync_ReturnsNull_WhenSpecDoesNotExist()
    {
        // Act
        var json = await _repository.GetFullDocumentJsonAsync("nonexistent", "1.0.0");

        // Assert
        json.Should().BeNull();
    }

    #endregion

    #region Audit Tests

    [Fact]
    public async Task AppendAuditEntryAsync_AppendsToAuditLog()
    {
        // Arrange
        var spec = CreateSampleSpec("test-spec-15", "1.0.0", "Draft");
        await _repository.CreateAsync(spec);

        var auditEntry = new AuditEntry
        {
            SpecId = "test-spec-15",
            Action = "CustomAction",
            Summary = "Custom audit entry",
            Actor = "test-user",
            Source = "Test"
        };

        // Act
        await _repository.AppendAuditEntryAsync("test-spec-15", auditEntry);

        // Assert
        var history = await _repository.GetAuditHistoryAsync("test-spec-15");
        history.Should().Contain(e => e.Action == "CustomAction");
    }

    [Fact]
    public async Task GetAuditHistoryAsync_ReturnsAllEntries()
    {
        // Arrange
        var spec = CreateSampleSpec("test-spec-16", "1.0.0", "Draft");
        var (created, etag) = await _repository.CreateAsync(spec);
        
        created.Metadata.Name = "Updated";
        await _repository.UpdateAsync("test-spec-16", created, etag);

        // Act
        var history = await _repository.GetAuditHistoryAsync("test-spec-16");

        // Assert
        history.Should().HaveCountGreaterOrEqualTo(2);
        history.Should().Contain(e => e.Action == "Created");
        history.Should().Contain(e => e.Action == "Updated");
    }

    [Fact]
    public async Task GetAuditHistoryAsync_ReturnsEmptyList_WhenNoAuditLog()
    {
        // Act
        var history = await _repository.GetAuditHistoryAsync("nonexistent");

        // Assert
        history.Should().BeEmpty();
    }

    #endregion

    #region JSON Persistence Tests

    [Fact]
    public async Task Repository_PersistsComplexSpecCorrectly()
    {
        // Arrange - Create a spec with complex nested structures
        var spec = CreateSampleSpec("complex-spec", "1.0.0", "Draft");
        spec.Traits.Add(new TraitDefinition
        {
            Key = "q2",
            AnswerType = "MultiSelect",
            QuestionText = "Select all that apply",
            Comment = "Choose multiple options",
            Required = false,
            Options = new List<string> { "Option A", "Option B" },
            Bounds = new TraitBounds
            {
                Min = 1,
                Max = 3
            }
        });

        // Act
        var (created, etag) = await _repository.CreateAsync(spec);
        var retrieved = await _repository.GetAsync("complex-spec", "1.0.0");

        // Assert
        retrieved.Should().NotBeNull();
        var doc = retrieved.Value.Document;
        
        doc.Traits.Should().HaveCount(2);
        doc.Traits[1].AnswerType.Should().Be("MultiSelect");
        doc.Traits[1].Options.Should().HaveCount(2);
        doc.Traits[1].Bounds.Should().NotBeNull();
        doc.Traits[1].Bounds!.Min.Should().Be(1);
        doc.Traits[1].Bounds!.Max.Should().Be(3);
    }

    [Fact]
    public async Task Repository_HandlesSpecialCharactersInJson()
    {
        // Arrange
        var spec = CreateSampleSpec("special-chars", "1.0.0", "Draft");
        spec.Metadata.Description = "Test with special chars: \"quotes\", 'apostrophes', \n newlines, \t tabs";
        spec.Traits[0].QuestionText = "What's your favorite <choice>?";

        // Act
        await _repository.CreateAsync(spec);
        var retrieved = await _repository.GetAsync("special-chars", "1.0.0");

        // Assert
        retrieved.Should().NotBeNull();
        retrieved.Value.Document.Metadata.Description.Should().Contain("\"quotes\"");
        retrieved.Value.Document.Traits[0].QuestionText.Should().Contain("<choice>");
    }

    #endregion

    #region Helper Methods

    private static DecisionSpecDocument CreateSampleSpec(string specId, string version, string status)
    {
        return new DecisionSpecDocument
        {
            SpecId = specId,
            Version = version,
            Status = status,
            Metadata = new DecisionSpecMetadata
            {
                Name = $"Test Spec {specId}",
                Description = "A test specification for unit testing",
                Owner = "test-user",
                Tags = new List<string> { "test", "automated" }
            },
            Traits = new List<TraitDefinition>
            {
                new()
                {
                    Key = "q1",
                    QuestionText = "What is your primary goal?",
                    AnswerType = "SingleSelect",
                    Required = true,
                    Options = new List<string> { "Option 1", "Option 2" }
                }
            }
        };
    }

    #endregion
}
