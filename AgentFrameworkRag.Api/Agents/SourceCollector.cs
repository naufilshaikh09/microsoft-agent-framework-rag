using System.Threading.Channels;
using AgentFrameworkRag.Api.Services;

namespace AgentFrameworkRag.Api.Agents;

// Bridge: streams structured source citations to the client when search_documents runs (not an MAF API).
public sealed class SourceCollector
{
    private readonly Channel<List<SourceReference>> _channel =
        Channel.CreateUnbounded<List<SourceReference>>(new UnboundedChannelOptions { SingleReader = true });

    private readonly List<SourceReference> _accumulated = [];
    private readonly HashSet<(string DocumentName, int ChunkIndex)> _seen = [];

    public ChannelReader<List<SourceReference>> Reader => _channel.Reader;

    public IReadOnlyList<SourceReference> AllSources => _accumulated;

    public void Publish(IReadOnlyList<SourceReference> sources)
    {
        if (sources.Count == 0)
            return;

        var batch = new List<SourceReference>();
        foreach (var source in sources)
        {
            var key = (source.DocumentName, source.ChunkIndex);
            if (!_seen.Add(key))
                continue;

            _accumulated.Add(source);
            batch.Add(source);
        }

        if (batch.Count > 0)
            _channel.Writer.TryWrite(batch);
    }

    public void Complete()
    {
        _channel.Writer.TryComplete();
    }
}
