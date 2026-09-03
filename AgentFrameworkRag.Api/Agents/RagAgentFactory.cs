using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace AgentFrameworkRag.Api.Agents;

public sealed class RagAgentFactory(
    IChatClient chatClient,
    RetrievalState retrievalState)
{
    private const string GeneralInstructions =
        "You are a helpful assistant. Answer questions clearly and concisely. " +
        "No documents have been uploaded yet — answer from your general knowledge.";

    private const string DocumentInstructions =
        """
        You are a document assistant. Answer the user's question using ONLY the provided document context.
        Do not use any knowledge outside the documents. If the answer is not found in the context,
        respond with exactly: "Sorry, I don't have knowledge about that based on the uploaded documents."
        """;

    private AIAgent? _generalAgent;

    public AIAgent GetGeneralAgent()
    {
        _generalAgent ??= chatClient
            .AsAIAgent(new ChatClientAgentOptions
            {
                Name = "general-assistant",
                ChatOptions = new() { Instructions = GeneralInstructions }
            })
            .AsBuilder()
            .UseOpenTelemetry(sourceName: "AgentFrameworkRag", configure: cfg => cfg.EnableSensitiveData = false)
            .Build();

        return _generalAgent;
    }

    public AIAgent CreateDocumentAgent()
    {
        return chatClient
            .AsAIAgent(new ChatClientAgentOptions
            {
                Name = "document-assistant",
                ChatOptions = new() { Instructions = DocumentInstructions },
                AIContextProviders = [new DocumentRagContextProvider(retrievalState)]
            })
            .AsBuilder()
            .UseOpenTelemetry(sourceName: "AgentFrameworkRag", configure: cfg => cfg.EnableSensitiveData = false)
            .Build();
    }

    public static IEnumerable<ChatMessage> BuildMessages(
        string message,
        IReadOnlyList<Services.HistoryMessage>? history,
        int historyWindow)
    {
        if (history is not null)
        {
            foreach (var h in history.TakeLast(historyWindow))
            {
                yield return h.Role.Equals("user", StringComparison.OrdinalIgnoreCase)
                    ? new ChatMessage(ChatRole.User, h.Content)
                    : new ChatMessage(ChatRole.Assistant, h.Content);
            }
        }

        yield return new ChatMessage(ChatRole.User, message);
    }
}
