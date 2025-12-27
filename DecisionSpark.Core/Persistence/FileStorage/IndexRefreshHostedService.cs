using DecisionSpark.Core.Persistence.FileStorage;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace DecisionSpark.Core.Persistence.FileStorage;

/// <summary>
/// Background service that periodically refreshes the DecisionSpec search index.
/// </summary>
public class IndexRefreshHostedService : BackgroundService
{
    private readonly FileSearchIndexer _indexer;
    private readonly ILogger<IndexRefreshHostedService> _logger;
    private readonly TimeSpan _refreshInterval = TimeSpan.FromMinutes(5);

    public IndexRefreshHostedService(
        FileSearchIndexer indexer,
        ILogger<IndexRefreshHostedService> logger)
    {
        _indexer = indexer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("IndexRefreshHostedService starting - initial index build");

        // Build index on startup
        await _indexer.RebuildIndexAsync(stoppingToken);

        _logger.LogInformation("Initial index build complete");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(_refreshInterval, stoppingToken);
                
                _logger.LogDebug("Refreshing DecisionSpec index");
                await _indexer.RebuildIndexAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing DecisionSpec index");
            }
        }

        _logger.LogInformation("IndexRefreshHostedService stopping");
    }
}
