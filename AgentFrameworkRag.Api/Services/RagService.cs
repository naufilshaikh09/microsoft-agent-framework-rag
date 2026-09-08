using System.Runtime.CompilerServices;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using AgentFrameworkRag.Api.Agents;
using AgentFrameworkRag.Api.Configuration;

namespace AgentFrameworkRag.Api.Services;

public record HistoryMessage(string Role, string Content);

public abstract record StreamChunk;
public record SourcesChunk(List<SourceReference> Sources) : StreamChunk;
public record TextChunk(string Delta) : StreamChunk;
public record ErrorChunk(string Message) : StreamChunk;
public record SourceReference(string DocumentName, int ChunkIndex, int PageNumber, string Excerpt, double Score);

public interface IRagService
{
    IAsyncEnumerable<StreamChunk> StreamChatAsync(string message, IReadOnlyList<HistoryMessage>? history = null, CancellationToken ct = default);
    Task<(string Reply, IReadOnlyList<SourceReference> Sources)> ChatAsync(string message, IReadOnlyList<HistoryMessage>? history = null, CancellationToken ct = default);
    Task<int> IndexDocumentAsync(string name, IReadOnlyList<PagedTextSegment> segments, CancellationToken ct = default);
    Task<bool> DeleteDocumentAsync(string name, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetDocumentNamesAsync(CancellationToken ct = default);
}

public sealed class RagService : IRagService
{
    // MAF: picks agent and runs RunStreamingAsync / RunAsync. Does not pre-fetch documents.
    // SSE source multiplexing is app plumbing (SourceCollector). See docs/FEATURES.md.

    private readonly DocumentRegistry _registry;
    private readonly DocumentIndexerService _indexer;
    private readonly RagAgentFactory _agentFactory;
    private readonly ChatRequestContext _requestContext;
    private readonly SourceCollector _sourceCollector;
    private readonly RagOptions _opts;
    private readonly ILogger<RagService> _logger;

    public RagService(
        DocumentRegistry registry,
        DocumentIndexerService indexer,
        RagAgentFactory agentFactory,
        ChatRequestContext requestContext,
        SourceCollector sourceCollector,
        IOptions<RagOptions> opts,
        ILogger<RagService> logger)
    {
        _registry = registry;
        _indexer = indexer;
        _agentFactory = agentFactory;
        _requestContext = requestContext;
        _sourceCollector = sourceCollector;
        _opts = opts.Value;
        _logger = logger;
    }

    public async IAsyncEnumerable<StreamChunk> StreamChatAsync(
        string message,
        IReadOnlyList<HistoryMessage>? history = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        _logger.LogInformation("StreamChat: {Message}", message);

        ChatRunPlan? plan = null;
        string? buildError = null;
        try
        {
            plan = await BuildRunPlanAsync(message, history, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build chat context");
            buildError = "Failed to start chat. Please try again.";
        }

        if (buildError is not null)
        {
            yield return new SourcesChunk([]);
            yield return new ErrorChunk(buildError);
            yield break;
        }

        var agentStream = plan!.Agent.RunStreamingAsync(plan.Messages, plan.Session, cancellationToken: ct);
        await using var enumerator = agentStream.GetAsyncEnumerator();

        try
        {
            Task<bool>? moveNextTask = null;

            while (true)
            {
                while (_sourceCollector.Reader.TryRead(out var sources))
                    yield return new SourcesChunk(sources);

                // Only one MoveNextAsync may be in flight — starting a second while sources
                // arrive first deadlocks the agent stream enumerator and hangs the SSE response.
                moveNextTask ??= enumerator.MoveNextAsync().AsTask();

                if (!moveNextTask.IsCompleted)
                {
                    var sourceTask = _sourceCollector.Reader.WaitToReadAsync(ct).AsTask();
                    if (await Task.WhenAny(moveNextTask, sourceTask) == sourceTask)
                        continue;
                }

                if (!await moveNextTask)
                    break;

                moveNextTask = null;

                var update = enumerator.Current;
                if (!string.IsNullOrEmpty(update.Text))
                    yield return new TextChunk(update.Text);
            }

            while (_sourceCollector.Reader.TryRead(out var remaining))
                yield return new SourcesChunk(remaining);
        }
        finally
        {
            _sourceCollector.Complete();
        }
    }

    public async Task<(string Reply, IReadOnlyList<SourceReference> Sources)> ChatAsync(
        string message,
        IReadOnlyList<HistoryMessage>? history = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Chat: {Message}", message);
        var plan = await BuildRunPlanAsync(message, history, ct);

        try
        {
            var response = await plan.Agent.RunAsync(plan.Messages, plan.Session, cancellationToken: ct);
            return (response.Text ?? string.Empty, _sourceCollector.AllSources.ToList());
        }
        finally
        {
            _sourceCollector.Complete();
        }
    }

    public Task<int> IndexDocumentAsync(string name, IReadOnlyList<PagedTextSegment> segments, CancellationToken ct = default)
        => _indexer.IndexAsync(name, segments, ct);

    public Task<bool> DeleteDocumentAsync(string name, CancellationToken ct = default)
        => _indexer.DeleteAsync(name, ct);

    public Task<IReadOnlyList<string>> GetDocumentNamesAsync(CancellationToken ct = default)
        => Task.FromResult(_registry.GetAll());

    private async Task<ChatRunPlan> BuildRunPlanAsync(
        string message,
        IReadOnlyList<HistoryMessage>? history,
        CancellationToken ct)
    {
        _requestContext.Message = message;
        _requestContext.History = history;

        var messages = RagAgentFactory.BuildMessages(message, history, _opts.HistoryWindow).ToList();

        var agent = _registry.HasDocuments
            ? _agentFactory.GetDocumentAgent()
            : _agentFactory.GetGeneralAgent();

        var session = await agent.CreateSessionAsync(ct);
        return new ChatRunPlan(agent, session, messages);
    }

    private sealed record ChatRunPlan(AIAgent Agent, AgentSession Session, List<ChatMessage> Messages);
}
