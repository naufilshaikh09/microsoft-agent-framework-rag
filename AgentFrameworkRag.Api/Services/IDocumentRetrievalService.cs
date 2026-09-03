namespace AgentFrameworkRag.Api.Services;

public interface IDocumentRetrievalService
{
    Task<RetrievalResult> RetrieveAsync(
        string message,
        IReadOnlyList<HistoryMessage>? history,
        CancellationToken ct = default);
}
