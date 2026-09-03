using Grpc.Core;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace AgentFrameworkRag.Api.Services;

/// <summary>
/// Repopulates DocumentRegistry from Qdrant on restart so HasDocuments is accurate
/// after vectors survive a server restart.
/// </summary>
public sealed class DocumentRegistrySeeder(
    QdrantClient qdrant,
    DocumentRegistry registry,
    ILogger<DocumentRegistrySeeder> logger) : IHostedService
{
    // Must match the collection name in RagService
    private const string CollectionName = "documents";

    // CommunityToolkit.VectorData.Qdrant stores C# property names as-is in the payload
    private const string DocumentNameField = "DocumentName";

    private const int MaxRetries = 5;
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(1);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Seeding DocumentRegistry from Qdrant...");

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                await SeedAsync(cancellationToken);
                return;
            }
            catch (RpcException ex) when (ex.StatusCode == StatusCode.NotFound)
            {
                logger.LogInformation("Qdrant collection not found — registry starts empty");
                return;
            }
            catch (Exception ex) when (attempt < MaxRetries && !cancellationToken.IsCancellationRequested)
            {
                var delay = InitialDelay * Math.Pow(2, attempt - 1); // exponential backoff
                logger.LogWarning(ex,
                    "Qdrant not ready (attempt {Attempt}/{Max}), retrying in {Delay:g}...",
                    attempt, MaxRetries, delay);
                await Task.Delay(delay, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to seed DocumentRegistry from Qdrant — registry starts empty");
                return;
            }
        }
    }

    private async Task SeedAsync(CancellationToken cancellationToken)
    {
        var docToIds = new Dictionary<string, List<Guid>>(StringComparer.OrdinalIgnoreCase);
        PointId? offset = null;

        while (true)
        {
            var response = await qdrant.ScrollAsync(
                CollectionName,
                limit: 250,
                offset: offset,
                payloadSelector: true,
                vectorsSelector: false,
                cancellationToken: cancellationToken);

            var points = response.Result;
            if (points.Count == 0) break;

            foreach (var point in points)
            {
                if (point.Payload.TryGetValue(DocumentNameField, out var nameVal) &&
                    nameVal.KindCase == Value.KindOneofCase.StringValue)
                {
                    var name = nameVal.StringValue;
                    var id = new Guid(point.Id.Uuid);
                    if (!docToIds.TryGetValue(name, out var ids))
                        docToIds[name] = ids = [];
                    ids.Add(id);
                }
            }

            offset = points[points.Count - 1].Id;
            if (points.Count < 250) break;
        }

        foreach (var (name, ids) in docToIds)
            registry.Register(name, ids);

        logger.LogInformation("Seeded {Count} documents from Qdrant", docToIds.Count);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
