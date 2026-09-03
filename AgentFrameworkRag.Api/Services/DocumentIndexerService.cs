using Microsoft.Extensions.AI;
using Microsoft.Extensions.VectorData;
using AgentFrameworkRag.Api.Models;

namespace AgentFrameworkRag.Api.Services;

public sealed class DocumentIndexerService(
    IEmbeddingGenerator<string, Embedding<float>> embedder,
    VectorStore vectorStore,
    DocumentRegistry registry,
    ILogger<DocumentIndexerService> logger)
{
    public const string CollectionName = "documents";

    public async Task<int> IndexAsync(
        string name,
        IReadOnlyList<PagedTextSegment> segments,
        CancellationToken ct = default)
    {
        if (registry.IsRegistered(name))
            throw new InvalidOperationException($"Document '{name}' is already indexed. Delete it first to re-upload.");

        var annotatedChunks = TextChunker.ChunkPaged(segments);
        logger.LogInformation("Indexing document: {Name} ({ChunkCount} chunks)", name, annotatedChunks.Count);

        if (annotatedChunks.Count == 0)
        {
            logger.LogWarning("Document {Name} produced no chunks — skipping", name);
            return 0;
        }

        var contents = annotatedChunks.Select(c => c.Content).ToList();
        var embeddings = await embedder.GenerateAsync(contents, cancellationToken: ct);
        var collection = vectorStore.GetCollection<Guid, DocumentChunk>(CollectionName);
        await collection.EnsureCollectionExistsAsync(ct);

        var chunkIds = new List<Guid>(annotatedChunks.Count);
        for (int i = 0; i < annotatedChunks.Count; i++)
        {
            var id = Guid.NewGuid();
            chunkIds.Add(id);
            await collection.UpsertAsync(new DocumentChunk
            {
                Id = id,
                DocumentName = name,
                Content = annotatedChunks[i].Content,
                ChunkIndex = i,
                PageNumber = annotatedChunks[i].PageNumber,
                StartOffset = annotatedChunks[i].StartOffset,
                Embedding = embeddings[i].Vector
            }, cancellationToken: ct);
        }

        registry.Register(name, chunkIds);
        logger.LogInformation("Indexed {Name}: {ChunkCount} chunks stored", name, annotatedChunks.Count);
        return annotatedChunks.Count;
    }

    public async Task<bool> DeleteAsync(string name, CancellationToken ct = default)
    {
        var chunkIds = registry.GetChunkIds(name);
        if (chunkIds.Count == 0) return false;

        var collection = vectorStore.GetCollection<Guid, DocumentChunk>(CollectionName);
        foreach (var id in chunkIds)
            await collection.DeleteAsync(id, cancellationToken: ct);

        registry.Unregister(name);
        logger.LogInformation("Deleted document {Name}: {Count} chunks removed", name, chunkIds.Count);
        return true;
    }
}
