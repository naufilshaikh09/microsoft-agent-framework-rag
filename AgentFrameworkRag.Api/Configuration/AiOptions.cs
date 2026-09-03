namespace AgentFrameworkRag.Api.Configuration;

public sealed class AiOptions
{
    public const string SectionName = "Ai";

    /// <summary>"OpenAI" or "AzureOpenAI"</summary>
    public string Provider { get; init; } = "OpenAI";

    public OpenAiOptions OpenAI { get; init; } = new();
    public AzureOpenAiOptions AzureOpenAI { get; init; } = new();
    public VectorStoreOptions VectorStore { get; init; } = new();
}

public sealed class OpenAiOptions
{
    /// <summary>Set via user secrets: Ai:OpenAI:ApiKey</summary>
    public string ApiKey { get; init; } = string.Empty;
    public string ChatModel { get; init; } = "gpt-4o";
    public string EmbeddingModel { get; init; } = "text-embedding-3-small";
}

public sealed class AzureOpenAiOptions
{
    /// <summary>Set via user secrets: Ai:AzureOpenAI:ApiKey</summary>
    public string ApiKey { get; init; } = string.Empty;
    public string Endpoint { get; init; } = string.Empty;
    public string ChatDeployment { get; init; } = "gpt-4o";
    public string EmbeddingDeployment { get; init; } = "text-embedding-3-small";
}

public sealed class VectorStoreOptions
{
    /// <summary>"InMemory" (default) or "Qdrant"</summary>
    public string Provider { get; init; } = "InMemory";
}

public sealed class RagOptions
{
    public const string SectionName = "Ai:Rag";

    /// <summary>Final number of chunks injected into the prompt.</summary>
    public int TopK { get; init; } = 6;

    /// <summary>Initial over-fetch before dedup/diversify. Must be >= TopK.</summary>
    public int CandidateK { get; init; } = 20;

    /// <summary>Approximate token budget for all context chunks combined.</summary>
    public int MaxContextTokens { get; init; } = 3000;

    /// <summary>Minimum cosine similarity score [0,1] to accept a chunk.</summary>
    public double MinRelevanceScore { get; init; } = 0.3;

    /// <summary>Maximum number of recent history turns passed to the LLM.</summary>
    public int HistoryWindow { get; init; } = 10;

    /// <summary>Max chunks taken from a single document (diversity cap).</summary>
    public int MaxChunksPerDocument { get; init; } = 3;
}
