using Microsoft.Agents.AI;
using AgentFrameworkRag.Api.Services;

namespace AgentFrameworkRag.Api.Agents;

// Bridge: connects MAF TextSearchProvider to DocumentRetrievalService + SourceCollector (SSE citations).
// See docs/FEATURES.md.
public sealed class DocumentSearchAdapter(
    IDocumentRetrievalService retrieval,
    ChatRequestContext requestContext,
    SourceCollector sourceCollector,
    ILogger<DocumentSearchAdapter> logger)
{
    public async Task<IEnumerable<TextSearchProvider.TextSearchResult>> SearchAsync(
        string query,
        CancellationToken ct = default)
    {
        logger.LogDebug("search_documents invoked with query: {Query}", query);

        var result = await retrieval.RetrieveAsync(query, requestContext.History, ct);
        sourceCollector.Publish(result.Sources);

        if (!result.HasRelevantContext)
            return [];

        return result.Chunks.Select(chunk => new TextSearchProvider.TextSearchResult
        {
            SourceName = chunk.DocumentName,
            Text = chunk.Content,
            RawRepresentation = chunk
        });
    }
}
