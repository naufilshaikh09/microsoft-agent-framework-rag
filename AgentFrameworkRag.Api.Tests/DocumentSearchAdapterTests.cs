using AgentFrameworkRag.Api.Agents;
using AgentFrameworkRag.Api.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentFrameworkRag.Api.Tests;

public class DocumentSearchAdapterTests
{
    [Fact]
    public async Task SearchAsync_MapsChunksAndPublishesSources()
    {
        var retrieval = new FakeRetrievalService();
        var requestContext = new ChatRequestContext { History = [] };
        var sourceCollector = new SourceCollector();
        var adapter = new DocumentSearchAdapter(
            retrieval,
            requestContext,
            sourceCollector,
            NullLogger<DocumentSearchAdapter>.Instance);

        retrieval.NextResult = new RetrievalResult(
            Sources:
            [
                new SourceReference("report.pdf", 0, 2, "Revenue grew", 0.91)
            ],
            Chunks:
            [
                new RetrievedChunk("report.pdf", 0, 2, "Revenue grew by 12% year over year.", 0.91)
            ],
            ContextText: "Revenue grew by 12% year over year.",
            DocumentNames: "report.pdf",
            HasRelevantContext: true);

        var results = (await adapter.SearchAsync("revenue growth", CancellationToken.None)).ToList();

        Assert.Single(results);
        Assert.Equal("report.pdf", results[0].SourceName);
        Assert.Equal("Revenue grew by 12% year over year.", results[0].Text);
        Assert.Single(sourceCollector.AllSources);
        Assert.Equal(0, sourceCollector.AllSources[0].ChunkIndex);
    }

    [Fact]
    public async Task SearchAsync_NoRelevantContext_ReturnsEmptyAndSkipsSources()
    {
        var retrieval = new FakeRetrievalService();
        var sourceCollector = new SourceCollector();
        var adapter = new DocumentSearchAdapter(
            retrieval,
            new ChatRequestContext(),
            sourceCollector,
            NullLogger<DocumentSearchAdapter>.Instance);

        retrieval.NextResult = new RetrievalResult([], [], string.Empty, "report.pdf", false);

        var results = await adapter.SearchAsync("unrelated topic", CancellationToken.None);

        Assert.Empty(results);
        Assert.Empty(sourceCollector.AllSources);
    }

    [Fact]
    public async Task SearchAsync_PassesRequestHistoryToRetrieval()
    {
        var retrieval = new FakeRetrievalService();
        var history = new List<HistoryMessage> { new("user", "Tell me about Q3") };
        var requestContext = new ChatRequestContext { History = history };
        var adapter = new DocumentSearchAdapter(
            retrieval,
            requestContext,
            new SourceCollector(),
            NullLogger<DocumentSearchAdapter>.Instance);

        retrieval.NextResult = new RetrievalResult([], [], string.Empty, string.Empty, false);

        await adapter.SearchAsync("What about revenue?", CancellationToken.None);

        Assert.Same(history, retrieval.LastHistory);
        Assert.Equal("What about revenue?", retrieval.LastMessage);
    }

    private sealed class FakeRetrievalService : IDocumentRetrievalService
    {
        public RetrievalResult NextResult { get; set; } =
            new([], [], string.Empty, string.Empty, false);

        public string? LastMessage { get; private set; }
        public IReadOnlyList<HistoryMessage>? LastHistory { get; private set; }

        public Task<RetrievalResult> RetrieveAsync(
            string message,
            IReadOnlyList<HistoryMessage>? history,
            CancellationToken ct = default)
        {
            LastMessage = message;
            LastHistory = history;
            return Task.FromResult(NextResult);
        }
    }
}
