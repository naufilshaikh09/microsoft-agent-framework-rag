using Microsoft.Extensions.VectorData;

namespace AgentFrameworkRag.Api.Models;

public sealed class DocumentChunk
{
    [VectorStoreKey]
    public Guid Id { get; set; } = Guid.NewGuid();

    [VectorStoreData]
    public string DocumentName { get; set; } = string.Empty;

    [VectorStoreData]
    public string Content { get; set; } = string.Empty;

    [VectorStoreData]
    public int ChunkIndex { get; set; }

    [VectorStoreData]
    public int PageNumber { get; set; }

    [VectorStoreData]
    public int StartOffset { get; set; }

    // text-embedding-3-small = 1536 dimensions; cosine similarity so score is always in [0,1] (higher = better)
    [VectorStoreVector(1536, DistanceFunction = DistanceFunction.CosineSimilarity)]
    public ReadOnlyMemory<float> Embedding { get; set; }
}
