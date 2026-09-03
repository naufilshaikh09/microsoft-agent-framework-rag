using System.Collections.Concurrent;

namespace AgentFrameworkRag.Api.Services;

public sealed class DocumentRegistry
{
    private readonly ConcurrentDictionary<string, List<Guid>> _docs = new(StringComparer.OrdinalIgnoreCase);

    public void Register(string name, IEnumerable<Guid> chunkIds) => _docs[name] = chunkIds.ToList();

    public bool IsRegistered(string name) => _docs.ContainsKey(name);

    public IReadOnlyList<Guid> GetChunkIds(string name)
        => _docs.TryGetValue(name, out var ids) ? ids : [];

    public bool Unregister(string name) => _docs.TryRemove(name, out _);

    public IReadOnlyList<string> GetAll() => [.. _docs.Keys.Order()];

    public bool HasDocuments => !_docs.IsEmpty;
}
