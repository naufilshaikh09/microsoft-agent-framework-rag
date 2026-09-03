using AgentFrameworkRag.Api.Services;

namespace AgentFrameworkRag.Api.Endpoints;

public static class DocumentEndpoints
{
    public static IEndpointRouteBuilder MapDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/documents").WithTags("Documents");

        group.MapGet("/", async (IRagService rag, CancellationToken ct) =>
        {
            var names = await rag.GetDocumentNamesAsync(ct);
            return Results.Ok(names);
        })
        .WithName("ListDocuments")
        .WithSummary("List indexed document names");

        group.MapPost("/upload", async (
            IFormFile file,
            IEnumerable<ITextExtractor> extractors,
            IRagService rag,
            ILoggerFactory loggerFactory,
            CancellationToken ct) =>
        {
            var logger = loggerFactory.CreateLogger("DocumentUpload");
            try
            {
                var extractor = extractors.FirstOrDefault(e => e.CanHandle(file.FileName));
                if (extractor is null)
                    return Results.BadRequest(new { Error = $"Unsupported file type '{Path.GetExtension(file.FileName)}'. Supported: .txt, .md, .csv, .pdf" });

                IReadOnlyList<PagedTextSegment> segments;
                try
                {
                    using var stream = file.OpenReadStream();
                    segments = await extractor.ExtractPagedAsync(stream, file.FileName, ct);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Text extraction failed for {FileName}", file.FileName);
                    return Results.BadRequest(new { Error = $"Could not read '{file.FileName}': {ex.Message}" });
                }

                if (segments.Count == 0 || segments.All(s => string.IsNullOrWhiteSpace(s.Text)))
                    return Results.UnprocessableEntity(new { Error = "Could not extract any text from the file. It may be a scanned image PDF with no selectable text." });

                int chunkCount;
                try
                {
                    chunkCount = await rag.IndexDocumentAsync(file.FileName, segments, ct);
                }
                catch (InvalidOperationException ex)
                {
                    return Results.Conflict(new { Error = ex.Message });
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Indexing failed for {FileName}", file.FileName);
                    return Results.Problem(title: "Indexing failed", detail: ex.Message, statusCode: 500);
                }

                return Results.Accepted("/api/documents", new { file.FileName, Status = "Indexed", ChunkCount = chunkCount });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Unexpected error uploading {FileName}", file.FileName);
                return Results.Problem(title: "Upload failed", detail: ex.Message, statusCode: 500);
            }
        })
        .WithName("UploadDocument")
        .WithSummary("Upload and index a document (.txt, .md, .csv, .pdf)")
        .DisableAntiforgery();

        group.MapDelete("/{documentName}", async (string documentName, IRagService rag, CancellationToken ct) =>
        {
            var deleted = await rag.DeleteDocumentAsync(documentName, ct);
            return deleted ? Results.NoContent() : Results.NotFound();
        })
        .WithName("DeleteDocument")
        .WithSummary("Remove a document and its indexed chunks");

        return app;
    }
}
