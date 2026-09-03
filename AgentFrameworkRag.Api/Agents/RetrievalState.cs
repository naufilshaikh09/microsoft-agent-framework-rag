using AgentFrameworkRag.Api.Services;

namespace AgentFrameworkRag.Api.Agents;

/// <summary>
/// Scoped per-request state shared between RagService and DocumentRagContextProvider.
/// </summary>
public sealed class RetrievalState
{
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<HistoryMessage>? History { get; set; }
    public RetrievalResult? Result { get; set; }
}
