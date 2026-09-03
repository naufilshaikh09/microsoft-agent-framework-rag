using System.Text;
using System.Text.Json;
using AgentFrameworkRag.Api.Services;

namespace AgentFrameworkRag.Api.Endpoints;

public static class ChatEndpoints
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/chat").WithTags("Chat");

        group.MapPost("/", async (ChatRequest req, IRagService rag, CancellationToken ct) =>
        {
            var (reply, sources) = await rag.ChatAsync(req.Message, req.History, ct);
            var sourceDtos = sources.Select(s => new SourceChunk(s.DocumentName, s.ChunkIndex, s.PageNumber, s.Excerpt, s.Score)).ToList();
            return Results.Ok(new ChatResponse(reply, sourceDtos));
        })
        .WithName("Chat")
        .WithSummary("Send a message and receive a complete response");

        group.MapPost("/stream", (ChatRequest req, IRagService rag, HttpContext ctx, CancellationToken ct) =>
        {
            ctx.Response.Headers.ContentType = "text/event-stream";
            ctx.Response.Headers.CacheControl = "no-cache";
            ctx.Response.Headers.Connection = "keep-alive";

            return Results.Stream(async stream =>
            {
                await foreach (var chunk in rag.StreamChatAsync(req.Message, req.History, ct))
                {
                    string line;
                    if (chunk is SourcesChunk sc)
                    {
                        var sourceDtos = sc.Sources.Select(s => new SourceChunk(s.DocumentName, s.ChunkIndex, s.PageNumber, s.Excerpt, s.Score));
                        var json = JsonSerializer.Serialize(sourceDtos, JsonOpts);
                        line = $"event: sources\ndata: {json}\n\n";
                    }
                    else if (chunk is TextChunk tc)
                    {
                        if (string.IsNullOrEmpty(tc.Delta)) continue;
                        line = $"data: {tc.Delta}\n\n";
                    }
                    else if (chunk is ErrorChunk ec)
                    {
                        var json = JsonSerializer.Serialize(new { error = ec.Message }, JsonOpts);
                        line = $"event: error\ndata: {json}\n\n";
                    }
                    else continue;

                    await stream.WriteAsync(Encoding.UTF8.GetBytes(line), ct);
                    await stream.FlushAsync(ct);
                }
                await stream.WriteAsync("data: [DONE]\n\n"u8.ToArray(), ct);
                await stream.FlushAsync(ct);
            }, "text/event-stream");
        })
        .WithName("ChatStream")
        .WithSummary("Stream a chat response via SSE");

        return app;
    }
}

public record SourceChunk(string DocumentName, int ChunkIndex, int PageNumber, string Excerpt, double Score);
public record ChatRequest(string Message, IReadOnlyList<HistoryMessage>? History = null);
public record ChatResponse(string Reply, IReadOnlyList<SourceChunk>? Sources = null);
