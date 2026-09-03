using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgentFrameworkRag.Api.Agents;

/// <summary>
/// Injects pre-computed document context from <see cref="RetrievalState"/> into the agent invocation.
/// </summary>
public sealed class DocumentRagContextProvider(RetrievalState retrievalState) : MessageAIContextProvider
{
    protected override ValueTask<IEnumerable<ChatMessage>> ProvideMessagesAsync(
        InvokingContext context,
        CancellationToken cancellationToken = default)
    {
        var result = retrievalState.Result;
        if (result is null || !result.HasRelevantContext)
            return new ValueTask<IEnumerable<ChatMessage>>([]);

        var contextMessage = $"""
            Indexed documents: {result.DocumentNames}

            Context from uploaded documents:
            {result.ContextText}
            """;

        IEnumerable<ChatMessage> messages =
        [
            new ChatMessage(ChatRole.User, contextMessage)
        ];

        return new ValueTask<IEnumerable<ChatMessage>>(messages);
    }
}
