using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.VectorData;
using AgentFrameworkRag.Api.Configuration;
using AgentFrameworkRag.Api.Models;

namespace AgentFrameworkRag.Api.Services;

public sealed record RetrievalResult(
    IReadOnlyList<SourceReference> Sources,
    string ContextText,
    string DocumentNames,
    bool HasRelevantContext);

public sealed class DocumentRetrievalService(
    IEmbeddingGenerator<string, Embedding<float>> embedder,
    VectorStore vectorStore,
    DocumentRegistry registry,
    QueryContextualizer contextualizer,
    IOptions<RagOptions> opts,
    ILogger<DocumentRetrievalService> logger)
{
    public async Task<RetrievalResult> RetrieveAsync(
        string message,
        IReadOnlyList<HistoryMessage>? history,
        CancellationToken ct = default)
    {
        if (!registry.HasDocuments)
        {
            return new RetrievalResult([], string.Empty, string.Empty, false);
        }

        var queryForRetrieval = await contextualizer.ContextualizeAsync(message, history, ct);
        var queryEmbedding = await embedder.GenerateVectorAsync(queryForRetrieval, cancellationToken: ct);

        var collection = vectorStore.GetCollection<Guid, DocumentChunk>(DocumentIndexerService.CollectionName);
        await collection.EnsureCollectionExistsAsync(ct);

        var candidates = new List<(DocumentChunk Chunk, double Score)>();
        await foreach (var result in collection.SearchAsync(queryEmbedding, opts.Value.CandidateK, cancellationToken: ct))
        {
            if (!result.Score.HasValue || result.Score.Value < opts.Value.MinRelevanceScore) continue;
            if (string.IsNullOrWhiteSpace(result.Record.Content)) continue;
            candidates.Add((result.Record, result.Score.Value));
        }

        candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
        var selected = DiversifyAndSelect(candidates, opts.Value.TopK, opts.Value.MaxChunksPerDocument);

        if (selected.Count == 0)
        {
            return new RetrievalResult(
                [],
                string.Empty,
                string.Join(", ", registry.GetAll()),
                false);
        }

        var contextParts = new List<string>();
        var sources = new List<SourceReference>();
        int usedTokens = 0;
        var seenSourceKeys = new HashSet<(string, int)>();

        foreach (var (chunk, score) in selected)
        {
            int chunkTokens = TokenEstimator.Estimate(chunk.Content);
            if (usedTokens + chunkTokens > opts.Value.MaxContextTokens) break;

            contextParts.Add(chunk.Content);
            usedTokens += chunkTokens;

            var key = (chunk.DocumentName, chunk.PageNumber);
            if (seenSourceKeys.Add(key))
            {
                var excerpt = chunk.Content[..Math.Min(150, chunk.Content.Length)];
                sources.Add(new SourceReference(
                    chunk.DocumentName,
                    chunk.ChunkIndex,
                    chunk.PageNumber,
                    excerpt,
                    Math.Round(score, 3)));
            }
        }

        if (contextParts.Count == 0)
        {
            return new RetrievalResult(
                [],
                string.Empty,
                string.Join(", ", registry.GetAll()),
                false);
        }

        var docNames = string.Join(", ", registry.GetAll());
        var context = string.Join("\n\n---\n\n", contextParts);

        logger.LogDebug(
            "Retrieved {SourceCount} sources ({ChunkCount} chunks) for query",
            sources.Count,
            contextParts.Count);

        return new RetrievalResult(sources, context, docNames, true);
    }

    private static List<(DocumentChunk Chunk, double Score)> DiversifyAndSelect(
        List<(DocumentChunk Chunk, double Score)> sortedCandidates,
        int topK,
        int maxPerDoc)
    {
        var result = new List<(DocumentChunk, double)>(topK);
        var docCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in sortedCandidates)
        {
            if (result.Count >= topK) break;

            var docName = candidate.Chunk.DocumentName;
            docCounts.TryGetValue(docName, out var count);
            if (count >= maxPerDoc) continue;

            result.Add(candidate);
            docCounts[docName] = count + 1;
        }

        return result;
    }
}
