using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using AgentFrameworkRag.Api.Configuration;

namespace AgentFrameworkRag.Api.Agents;

// MAF: agent factory — instructions, TextSearchProvider (search_documents), tool iteration limits.
// See docs/FEATURES.md for the full MAF vs non-MAF map.
public sealed class RagAgentFactory(
    IChatClient chatClient,
    DocumentSearchAdapter searchAdapter,
    IOptions<RagOptions> opts,
    ILoggerFactory loggerFactory)
{
    private const string GeneralInstructions =
        "You are a helpful assistant. Answer questions clearly and concisely. " +
        "No documents have been uploaded yet — answer from your general knowledge.";

    private const string DocumentInstructions =
        """
        You are a document assistant. Uploaded documents are available for search.

        Use the search_documents tool when the user asks about content that may be in their uploaded files.
        For greetings, small talk, or questions clearly unrelated to uploaded documents, answer directly without searching.
        When search returns relevant passages, answer using only that information.
        Do not include source citations, document names, or [Source: ...] markers in your response — sources are shown separately in the UI.
        If search returns no relevant results, say you could not find that information in the uploaded documents.
        """;

    private AIAgent? _generalAgent;
    private AIAgent? _documentAgent;

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

    public AIAgent GetDocumentAgent()
    {
        _documentAgent ??= CreateDocumentChatClient()
            .AsAIAgent(new ChatClientAgentOptions
            {
                Name = "document-assistant",
                ChatOptions = new() { Instructions = DocumentInstructions },
                AIContextProviders = [CreateTextSearchProvider()]
            })
            .AsBuilder()
            .UseOpenTelemetry(sourceName: "AgentFrameworkRag", configure: cfg => cfg.EnableSensitiveData = false)
            .Build();

        return _documentAgent;
    }

    private IChatClient CreateDocumentChatClient() =>
        chatClient
            .AsBuilder()
            .UseFunctionInvocation(loggerFactory, configure: ficc =>
                ficc.MaximumIterationsPerRequest = opts.Value.MaxToolIterations)
            .Build();

    private TextSearchProvider CreateTextSearchProvider() =>
        new(
            searchAdapter.SearchAsync,
            new TextSearchProviderOptions
            {
                SearchTime = TextSearchProviderOptions.TextSearchBehavior.OnDemandFunctionCalling,
                FunctionToolName = "search_documents",
                FunctionToolDescription =
                    "Search uploaded documents for relevant passages. " +
                    "Provide a self-contained search query with enough context for follow-up questions.",
                CitationsPrompt = "Do not include citations or [Source: ...] markers in your answer."
            },
            loggerFactory);

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
