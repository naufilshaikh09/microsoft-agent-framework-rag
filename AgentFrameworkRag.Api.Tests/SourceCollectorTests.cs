using AgentFrameworkRag.Api.Agents;
using AgentFrameworkRag.Api.Services;

namespace AgentFrameworkRag.Api.Tests;

public class SourceCollectorTests
{
    [Fact]
    public void Publish_DeduplicatesByDocumentAndChunkIndex()
    {
        var collector = new SourceCollector();
        var first = new SourceReference("doc.pdf", 0, 1, "excerpt-a", 0.9);
        var duplicate = new SourceReference("doc.pdf", 0, 1, "excerpt-b", 0.8);
        var second = new SourceReference("doc.pdf", 1, 1, "excerpt-c", 0.7);

        collector.Publish([first, duplicate, second]);

        Assert.Equal(2, collector.AllSources.Count);
        Assert.Equal(first, collector.AllSources[0]);
        Assert.Equal(second, collector.AllSources[1]);
    }

    [Fact]
    public async Task Publish_WritesBatchToChannel()
    {
        var collector = new SourceCollector();
        var sources = new List<SourceReference>
        {
            new("notes.txt", 2, 1, "hello", 0.85)
        };

        collector.Publish(sources);

        Assert.True(await collector.Reader.WaitToReadAsync());
        Assert.True(collector.Reader.TryRead(out var batch));
        Assert.Single(batch);
        Assert.Equal("notes.txt", batch[0].DocumentName);
    }

    [Fact]
    public void Publish_EmptyList_DoesNothing()
    {
        var collector = new SourceCollector();

        collector.Publish([]);

        Assert.Empty(collector.AllSources);
        Assert.False(collector.Reader.TryRead(out _));
    }
}
