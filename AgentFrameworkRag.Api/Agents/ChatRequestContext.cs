using AgentFrameworkRag.Api.Services;

namespace AgentFrameworkRag.Api.Agents;

/// <summary>
/// Scoped per-request chat state shared between RagService and DocumentSearchAdapter.
/// </summary>
public sealed class ChatRequestContext
{
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<HistoryMessage>? History { get; set; }
}
