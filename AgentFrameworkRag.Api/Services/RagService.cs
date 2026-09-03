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
    private const string NoContextReply =
        "Sorry, I don't have knowledge about that based on the uploaded documents.";

    private readonly DocumentRegistry _registry;
    private readonly DocumentIndexerService _indexer;
    private readonly DocumentRetrievalService _retrieval;
    private readonly RagAgentFactory _agentFactory;
    private readonly RetrievalState _retrievalState;
    private readonly RagOptions _opts;
    private readonly ILogger<RagService> _logger;

    public RagService(
        DocumentRegistry registry,
        DocumentIndexerService indexer,
        DocumentRetrievalService retrieval,
        RagAgentFactory agentFactory,
        RetrievalState retrievalState,
        IOptions<RagOptions> opts,
        ILogger<RagService> logger)
    {
        _registry = registry;
        _indexer = indexer;
        _retrieval = retrieval;
        _agentFactory = agentFactory;
        _retrievalState = retrievalState;
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
            buildError = "Failed to retrieve context. Please try again.";
        }

        if (buildError is not null)
        {
            yield return new SourcesChunk([]);
            yield return new ErrorChunk(buildError);
            yield break;
        }

        yield return new SourcesChunk(plan!.Sources);

        if (plan.IsStatic)
        {
            yield return new TextChunk(plan.StaticReply);
            yield break;
        }

        await foreach (var update in plan.Agent.RunStreamingAsync(plan.Messages, plan.Session, cancellationToken: ct)
                                         .WithCancellation(ct))
        {
            if (!string.IsNullOrEmpty(update.Text))
                yield return new TextChunk(update.Text);
        }
    }

    public async Task<(string Reply, IReadOnlyList<SourceReference> Sources)> ChatAsync(
        string message,
        IReadOnlyList<HistoryMessage>? history = null,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Chat: {Message}", message);
        var plan = await BuildRunPlanAsync(message, history, ct);

        if (plan.IsStatic)
            return (plan.StaticReply, plan.Sources);

        var response = await plan.Agent.RunAsync(plan.Messages, plan.Session, cancellationToken: ct);
        return (response.Text ?? string.Empty, plan.Sources);
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
        var messages = RagAgentFactory.BuildMessages(message, history, _opts.HistoryWindow).ToList();

        if (!_registry.HasDocuments)
        {
            var agent = _agentFactory.GetGeneralAgent();
            var session = await agent.CreateSessionAsync(ct);
            return ChatRunPlan.ForAgent(agent, session, messages, []);
        }

        _retrievalState.Message = message;
        _retrievalState.History = history;

        var retrieval = await _retrieval.RetrieveAsync(message, history, ct);
        _retrievalState.Result = retrieval;

        if (!retrieval.HasRelevantContext)
            return ChatRunPlan.Static(NoContextReply, retrieval.Sources);

        var documentAgent = _agentFactory.CreateDocumentAgent();
        var documentSession = await documentAgent.CreateSessionAsync(ct);
        return ChatRunPlan.ForAgent(documentAgent, documentSession, messages, retrieval.Sources.ToList());
    }

    private sealed record ChatRunPlan(
        AIAgent Agent,
        AgentSession Session,
        List<ChatMessage> Messages,
        List<SourceReference> Sources,
        bool IsStatic,
        string StaticReply)
    {
        public static ChatRunPlan ForAgent(
            AIAgent agent,
            AgentSession session,
            List<ChatMessage> messages,
            List<SourceReference> sources)
            => new(agent, session, messages, sources, false, string.Empty);

        public static ChatRunPlan Static(string reply, IReadOnlyList<SourceReference> sources)
            => new(null!, null!, [], sources.ToList(), true, reply);
    }
}
