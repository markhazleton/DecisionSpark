using DecisionSpark.Core.Models.Configuration;
using DecisionSpark.Core.Persistence.FileStorage;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Text.Json;
using Xunit;

namespace DecisionSpark.Tests.Core;

/// <summary>
/// Tests for FileSearchIndexer covering index management, querying, and rebuild operations.
/// </summary>
public class FileSearchIndexerTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly DecisionSpecsOptions _options;
    private readonly Mock<ILogger<FileSearchIndexer>> _loggerMock;
    private readonly Mock<ILogger<DecisionSpecFileStore>> _fileStoreLoggerMock;
    private readonly DecisionSpecFileStore _fileStore;
    private readonly FileSearchIndexer _indexer;

    public FileSearchIndexerTests()
    {
        // Create temp directory for testing
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"DecisionSparkTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempDirectory);

        _options = new DecisionSpecsOptions
        {
            RootPath = _tempDirectory,
            IndexFileName = "DecisionSpecIndex.json",
            SoftDeleteRetentionDays = 30
        };

        _loggerMock = new Mock<ILogger<FileSearchIndexer>>();
        _fileStoreLoggerMock = new Mock<ILogger<DecisionSpecFileStore>>();

        var optionsWrapper = Options.Create(_options);
        _fileStore = new DecisionSpecFileStore(optionsWrapper, _fileStoreLoggerMock.Object);
        _indexer = new FileSearchIndexer(optionsWrapper, _loggerMock.Object, _fileStore);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task UpdateEntryAsync_ShouldAddNewEntry_WhenSpecDoesNotExist()
    {
        // Arrange
        var specId = "TEST_SPEC";
        var version = "100";
        var status = "Draft";
        var testSpec = CreateTestSpec(specId, version, status, "Test Spec", "TestOwner", 3);
        
        await _fileStore.WriteAsync(specId, version, status, testSpec, CancellationToken.None);

        // Act
        await _indexer.UpdateEntryAsync(specId, version, status, CancellationToken.None);

        // Assert
        var results = await _indexer.QueryAsync(cancellationToken: CancellationToken.None);
        results.Should().ContainSingle(e => e.SpecId == specId);
        
        var entry = results.First(e => e.SpecId == specId);
        entry.Name.Should().Be("Test Spec");
        entry.Owner.Should().Be("TestOwner");
        entry.Status.Should().Be(status);
        entry.Version.Should().Be(version);
        entry.TraitCount.Should().Be(3);
    }

    [Fact]
    public async Task UpdateEntryAsync_ShouldUpdateExistingEntry_WhenSpecExists()
    {
        // Arrange
        var specId = "TEST_SPEC";
        var version = "100";
        var status = "Draft";
        var testSpec1 = CreateTestSpec(specId, version, status, "Test Spec v1", "Owner1", 2);
        
        await _fileStore.WriteAsync(specId, version, status, testSpec1, CancellationToken.None);
        await _indexer.UpdateEntryAsync(specId, version, status, CancellationToken.None);

        // Update the spec
        var testSpec2 = CreateTestSpec(specId, version, status, "Test Spec v2", "Owner2", 5);
        await _fileStore.WriteAsync(specId, version, status, testSpec2, CancellationToken.None);

        // Act
        await _indexer.UpdateEntryAsync(specId, version, status, CancellationToken.None);

        // Assert
        var results = await _indexer.QueryAsync(cancellationToken: CancellationToken.None);
        results.Should().ContainSingle(e => e.SpecId == specId);
        
        var entry = results.First(e => e.SpecId == specId);
        entry.Name.Should().Be("Test Spec v2");
        entry.Owner.Should().Be("Owner2");
        entry.TraitCount.Should().Be(5);
    }

    [Fact]
    public async Task UpdateEntryAsync_ShouldHandleUnverifiedDraftFlag()
    {
        // Arrange
        var specId = "UNVERIFIED_SPEC";
        var version = "100";
        var status = "Draft";
        var testSpec = CreateTestSpec(specId, version, status, "Unverified Spec", "TestOwner", 2, unverified: true);
        
        await _fileStore.WriteAsync(specId, version, status, testSpec, CancellationToken.None);

        // Act
        await _indexer.UpdateEntryAsync(specId, version, status, CancellationToken.None);

        // Assert
        var results = await _indexer.QueryAsync(cancellationToken: CancellationToken.None);
        var entry = results.First(e => e.SpecId == specId);
        entry.HasUnverifiedDraft.Should().BeTrue();
    }

    [Fact]
    public async Task RemoveEntryAsync_ShouldRemoveEntry_WhenExists()
    {
        // Arrange
        var specId = "TEST_SPEC";
        var version = "100";
        var status = "Draft";
        var testSpec = CreateTestSpec(specId, version, status, "Test Spec", "TestOwner", 2);
        
        await _fileStore.WriteAsync(specId, version, status, testSpec, CancellationToken.None);
        await _indexer.UpdateEntryAsync(specId, version, status, CancellationToken.None);

        // Verify it exists
        var resultsBefore = await _indexer.QueryAsync(cancellationToken: CancellationToken.None);
        resultsBefore.Should().ContainSingle(e => e.SpecId == specId);

        // Act
        await _indexer.RemoveEntryAsync(specId, CancellationToken.None);

        // Assert
        var resultsAfter = await _indexer.QueryAsync(cancellationToken: CancellationToken.None);
        resultsAfter.Should().NotContain(e => e.SpecId == specId);
    }

    [Fact]
    public async Task QueryAsync_ShouldReturnAllEntries_WhenNoFiltersApplied()
    {
        // Arrange
        await CreateAndIndexTestSpec("SPEC_1", "100", "Draft", "Spec 1", "Owner1", 2);
        await CreateAndIndexTestSpec("SPEC_2", "100", "Published", "Spec 2", "Owner2", 3);
        await CreateAndIndexTestSpec("SPEC_3", "100", "InReview", "Spec 3", "Owner1", 1);

        // Act
        var results = await _indexer.QueryAsync(cancellationToken: CancellationToken.None);

        // Assert
        results.Should().HaveCount(3);
        results.Should().Contain(e => e.SpecId == "SPEC_1");
        results.Should().Contain(e => e.SpecId == "SPEC_2");
        results.Should().Contain(e => e.SpecId == "SPEC_3");
    }

    [Fact]
    public async Task QueryAsync_ShouldFilterByStatus()
    {
        // Arrange
        await CreateAndIndexTestSpec("SPEC_DRAFT", "100", "Draft", "Draft Spec", "Owner1", 2);
        await CreateAndIndexTestSpec("SPEC_PUBLISHED", "100", "Published", "Published Spec", "Owner2", 3);
        await CreateAndIndexTestSpec("SPEC_RETIRED", "100", "Retired", "Retired Spec", "Owner1", 1);

        // Act
        var results = await _indexer.QueryAsync(status: "Published", cancellationToken: CancellationToken.None);

        // Assert
        results.Should().ContainSingle();
        results.First().SpecId.Should().Be("SPEC_PUBLISHED");
        results.First().Status.Should().Be("Published");
    }

    [Fact]
    public async Task QueryAsync_ShouldFilterByOwner()
    {
        // Arrange
        await CreateAndIndexTestSpec("SPEC_1", "100", "Draft", "Spec 1", "Alice", 2);
        await CreateAndIndexTestSpec("SPEC_2", "100", "Draft", "Spec 2", "Bob", 3);
        await CreateAndIndexTestSpec("SPEC_3", "100", "Draft", "Spec 3", "Alice", 1);

        // Act
        var results = await _indexer.QueryAsync(owner: "Alice", cancellationToken: CancellationToken.None);

        // Assert
        results.Should().HaveCount(2);
        results.Should().OnlyContain(e => e.Owner == "Alice");
    }

    [Fact]
    public async Task QueryAsync_ShouldFilterBySearchTerm_InName()
    {
        // Arrange
        await CreateAndIndexTestSpec("FAMILY", "100", "Draft", "Family Saturday", "Owner1", 2);
        await CreateAndIndexTestSpec("TECH", "100", "Draft", "Tech Stack", "Owner2", 3);
        await CreateAndIndexTestSpec("VACATION", "100", "Draft", "Vacation Planning", "Owner1", 1);

        // Act
        var results = await _indexer.QueryAsync(searchTerm: "tech", cancellationToken: CancellationToken.None);

        // Assert
        results.Should().ContainSingle();
        results.First().SpecId.Should().Be("TECH");
    }

    [Fact]
    public async Task QueryAsync_ShouldFilterBySearchTerm_InSpecId()
    {
        // Arrange
        await CreateAndIndexTestSpec("FAMILY_SATURDAY", "100", "Draft", "Weekend Activity", "Owner1", 2);
        await CreateAndIndexTestSpec("TECH_ADVISOR", "100", "Draft", "Technology Choices", "Owner2", 3);
        await CreateAndIndexTestSpec("VACATION_PLANNER", "100", "Draft", "Vacation Ideas", "Owner1", 1);

        // Act
        var results = await _indexer.QueryAsync(searchTerm: "FAMILY", cancellationToken: CancellationToken.None);

        // Assert
        results.Should().ContainSingle();
        results.First().SpecId.Should().Be("FAMILY_SATURDAY");
    }

    [Fact]
    public async Task QueryAsync_ShouldApplyMultipleFilters()
    {
        // Arrange
        await CreateAndIndexTestSpec("SPEC_1", "100", "Draft", "Test Spec 1", "Alice", 2);
        await CreateAndIndexTestSpec("SPEC_2", "100", "Published", "Test Spec 2", "Alice", 3);
        await CreateAndIndexTestSpec("SPEC_3", "100", "Draft", "Test Spec 3", "Bob", 1);
        await CreateAndIndexTestSpec("SPEC_4", "100", "Draft", "Test Spec 4", "Alice", 2);

        // Act
        var results = await _indexer.QueryAsync(
            status: "Draft",
            owner: "Alice",
            searchTerm: "Test",
            cancellationToken: CancellationToken.None);

        // Assert
        results.Should().HaveCount(2);
        results.Should().OnlyContain(e => e.Status == "Draft" && e.Owner == "Alice" && e.Name.Contains("Test"));
    }

    [Fact]
    public async Task QueryAsync_ShouldReturnEntriesOrderedByUpdatedAtDescending()
    {
        // Arrange
        await CreateAndIndexTestSpec("SPEC_OLD", "100", "Draft", "Old Spec", "Owner1", 2);
        await Task.Delay(100); // Ensure different timestamps
        await CreateAndIndexTestSpec("SPEC_NEW", "100", "Draft", "New Spec", "Owner2", 3);
        await Task.Delay(100);
        await CreateAndIndexTestSpec("SPEC_NEWEST", "100", "Draft", "Newest Spec", "Owner3", 1);

        // Act
        var results = (await _indexer.QueryAsync(cancellationToken: CancellationToken.None)).ToList();

        // Assert
        results.Should().HaveCount(3);
        results[0].SpecId.Should().Be("SPEC_NEWEST");
        results[1].SpecId.Should().Be("SPEC_NEW");
        results[2].SpecId.Should().Be("SPEC_OLD");
    }

    [Fact]
    public async Task RebuildIndexAsync_ShouldRebuildFromFileSystem()
    {
        // Arrange - Create files directly without indexing
        var spec1 = CreateTestSpec("SPEC_1", "100", "Draft", "Spec 1", "Owner1", 2);
        var spec2 = CreateTestSpec("SPEC_2", "100", "Published", "Spec 2", "Owner2", 3);
        
        await _fileStore.WriteAsync("SPEC_1", "100", "Draft", spec1, CancellationToken.None);
        await _fileStore.WriteAsync("SPEC_2", "100", "Published", spec2, CancellationToken.None);

        // Act
        await _indexer.RebuildIndexAsync(CancellationToken.None);

        // Assert
        var results = await _indexer.QueryAsync(cancellationToken: CancellationToken.None);
        results.Should().HaveCount(2);
        results.Should().Contain(e => e.SpecId == "SPEC_1");
        results.Should().Contain(e => e.SpecId == "SPEC_2");
    }

    [Fact]
    public async Task RebuildIndexAsync_ShouldHandleMultipleStatuses()
    {
        // Arrange
        await _fileStore.WriteAsync("DRAFT_SPEC", "100", "Draft", CreateTestSpec("DRAFT_SPEC", "100", "Draft", "Draft", "Owner1", 1), CancellationToken.None);
        await _fileStore.WriteAsync("REVIEW_SPEC", "100", "InReview", CreateTestSpec("REVIEW_SPEC", "100", "InReview", "Review", "Owner2", 2), CancellationToken.None);
        await _fileStore.WriteAsync("PUBLISHED_SPEC", "100", "Published", CreateTestSpec("PUBLISHED_SPEC", "100", "Published", "Published", "Owner3", 3), CancellationToken.None);
        await _fileStore.WriteAsync("RETIRED_SPEC", "100", "Retired", CreateTestSpec("RETIRED_SPEC", "100", "Retired", "Retired", "Owner4", 4), CancellationToken.None);

        // Act
        await _indexer.RebuildIndexAsync(CancellationToken.None);

        // Assert
        var results = await _indexer.QueryAsync(cancellationToken: CancellationToken.None);
        results.Should().HaveCount(4);
        results.Should().Contain(e => e.SpecId == "DRAFT_SPEC" && e.Status == "Draft");
        results.Should().Contain(e => e.SpecId == "REVIEW_SPEC" && e.Status == "InReview");
        results.Should().Contain(e => e.SpecId == "PUBLISHED_SPEC" && e.Status == "Published");
        results.Should().Contain(e => e.SpecId == "RETIRED_SPEC" && e.Status == "Retired");
    }

    [Fact]
    public async Task RebuildIndexAsync_ShouldReplaceExistingIndex()
    {
        // Arrange
        await CreateAndIndexTestSpec("SPEC_1", "100", "Draft", "Spec 1", "Owner1", 2);
        await CreateAndIndexTestSpec("SPEC_2", "100", "Draft", "Spec 2", "Owner2", 3);

        // Delete one file directly
        var draftDir = Path.Combine(_tempDirectory, "draft");
        var fileToDelete = Path.Combine(draftDir, "SPEC_1.100.Draft.json");
        if (File.Exists(fileToDelete))
        {
            File.Delete(fileToDelete);
        }

        // Act
        await _indexer.RebuildIndexAsync(CancellationToken.None);

        // Assert
        var results = await _indexer.QueryAsync(cancellationToken: CancellationToken.None);
        results.Should().ContainSingle();
        results.First().SpecId.Should().Be("SPEC_2");
    }

    [Fact]
    public async Task RebuildIndexAsync_ShouldHandleEmptyDirectory()
    {
        // Act
        await _indexer.RebuildIndexAsync(CancellationToken.None);

        // Assert
        var results = await _indexer.QueryAsync(cancellationToken: CancellationToken.None);
        results.Should().BeEmpty();
    }

    [Fact]
    public async Task RebuildIndexAsync_ShouldSkipMalformedFiles()
    {
        // Arrange
        var validSpec = CreateTestSpec("VALID_SPEC", "100", "Draft", "Valid", "Owner1", 2);
        await _fileStore.WriteAsync("VALID_SPEC", "100", "Draft", validSpec, CancellationToken.None);

        // Create a malformed file directly
        var draftDir = Path.Combine(_tempDirectory, "draft");
        Directory.CreateDirectory(draftDir);
        await File.WriteAllTextAsync(Path.Combine(draftDir, "MALFORMED.json"), "{ invalid json");

        // Act
        await _indexer.RebuildIndexAsync(CancellationToken.None);

        // Assert
        var results = await _indexer.QueryAsync(cancellationToken: CancellationToken.None);
        results.Should().ContainSingle();
        results.First().SpecId.Should().Be("VALID_SPEC");
    }

    [Fact]
    public async Task IndexPersistence_ShouldSurviveIndexerRecreation()
    {
        // Arrange
        await CreateAndIndexTestSpec("SPEC_1", "100", "Draft", "Spec 1", "Owner1", 2);
        await CreateAndIndexTestSpec("SPEC_2", "100", "Published", "Spec 2", "Owner2", 3);

        // Act - Create new indexer instance (simulates app restart)
        var newIndexer = new FileSearchIndexer(
            Options.Create(_options),
            _loggerMock.Object,
            _fileStore);

        var results = await newIndexer.QueryAsync(cancellationToken: CancellationToken.None);

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(e => e.SpecId == "SPEC_1");
        results.Should().Contain(e => e.SpecId == "SPEC_2");
    }

    [Fact]
    public async Task ETagGeneration_ShouldBeConsistent()
    {
        // Arrange
        var specId = "ETAG_TEST";
        var version = "100";
        var status = "Draft";
        var testSpec = CreateTestSpec(specId, version, status, "ETag Test", "Owner1", 2);
        
        await _fileStore.WriteAsync(specId, version, status, testSpec, CancellationToken.None);
        await _indexer.UpdateEntryAsync(specId, version, status, CancellationToken.None);

        // Act
        var results1 = await _indexer.QueryAsync(cancellationToken: CancellationToken.None);
        var etag1 = results1.First(e => e.SpecId == specId).ETag;

        // Rebuild index
        await _indexer.RebuildIndexAsync(CancellationToken.None);

        var results2 = await _indexer.QueryAsync(cancellationToken: CancellationToken.None);
        var etag2 = results2.First(e => e.SpecId == specId).ETag;

        // Assert
        etag1.Should().Be(etag2);
        etag1.Should().NotBeNullOrEmpty();
    }

    // Helper methods

    private async Task CreateAndIndexTestSpec(string specId, string version, string status, string name, string owner, int traitCount)
    {
        var spec = CreateTestSpec(specId, version, status, name, owner, traitCount);
        await _fileStore.WriteAsync(specId, version, status, spec, CancellationToken.None);
        await _indexer.UpdateEntryAsync(specId, version, status, CancellationToken.None);
    }

    private string CreateTestSpec(string specId, string version, string status, string name, string owner, int traitCount, bool unverified = false)
    {
        var traits = Enumerable.Range(1, traitCount).Select(i => new
        {
            key = $"trait_{i}",
            question_text = $"Question {i}",
            answer_type = "choice",
            options = new[] { "option1", "option2" }
        }).ToArray();

        var spec = new
        {
            spec_id = specId,
            version = version,
            status = status,
            metadata = new
            {
                name = name,
                owner = owner,
                unverified = unverified
            },
            traits = traits,
            outcomes = new[]
            {
                new
                {
                    outcome_id = "OUTCOME_1",
                    selection_rules = new[] { "rule1" },
                    display_cards = new[] { new { type = "card" } }
                }
            }
        };

        return JsonSerializer.Serialize(spec, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = true
        });
    }
}
