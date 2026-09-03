using Microsoft.Extensions.AI;

namespace AgentFrameworkRag.Api.Services;

// Non-MAF: fixed retrieval preprocessing — rewrites follow-ups for better embeddings.
// Uses IChatClient directly (not AIAgent) because this is internal to search, not user-facing chat.
// See docs/FEATURES.md.
public sealed class QueryContextualizer(
    IChatClient chatClient,
    ILogger<QueryContextualizer> logger)
{
    private const string SystemPrompt =
        "Given a conversation history and a follow-up question, rewrite the follow-up into a " +
        "self-contained standalone question that includes all context needed to search a document. " +
        "Output ONLY the rewritten question — no explanation, no prefix, no punctuation changes beyond what is needed.";

    public async Task<string> ContextualizeAsync(
        string message,
        IReadOnlyList<HistoryMessage>? history,
        CancellationToken ct = default)
    {
        if (history is null || history.Count == 0)
            return message;

        try
        {
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, SystemPrompt)
            };

            foreach (var h in history.TakeLast(6))
                messages.Add(ToChatMessage(h));

            messages.Add(new ChatMessage(ChatRole.User, $"Follow-up: {message}"));

            var response = await chatClient.GetResponseAsync(messages, cancellationToken: ct);
            var rewritten = response.Text?.Trim();

            if (!string.IsNullOrWhiteSpace(rewritten) && rewritten != message)
            {
                logger.LogDebug("Query rewritten: '{Original}' → '{Rewritten}'", message, rewritten);
                return rewritten;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Query contextualization failed, using original message");
        }

        return message;
    }

    private static ChatMessage ToChatMessage(HistoryMessage msg) =>
        msg.Role.Equals("user", StringComparison.OrdinalIgnoreCase)
            ? new ChatMessage(ChatRole.User, msg.Content)
            : new ChatMessage(ChatRole.Assistant, msg.Content);
}
