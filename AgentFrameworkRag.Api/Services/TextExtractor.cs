using System.Text;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace AgentFrameworkRag.Api.Services;

public readonly record struct PagedTextSegment(int PageNumber, int StartOffset, string Text);

public interface ITextExtractor
{
    bool CanHandle(string fileName);
    Task<string> ExtractAsync(Stream stream, string fileName, CancellationToken ct = default);
    Task<IReadOnlyList<PagedTextSegment>> ExtractPagedAsync(Stream stream, string fileName, CancellationToken ct = default);
}

public sealed class PlainTextExtractor : ITextExtractor
{
    public bool CanHandle(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext is ".txt" or ".md" or ".csv";
    }

    public async Task<string> ExtractAsync(Stream stream, string fileName, CancellationToken ct = default)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return await reader.ReadToEndAsync(ct);
    }

    public async Task<IReadOnlyList<PagedTextSegment>> ExtractPagedAsync(Stream stream, string fileName, CancellationToken ct = default)
    {
        var text = await ExtractAsync(stream, fileName, ct);
        return [new PagedTextSegment(1, 0, text)];
    }
}

public sealed class PdfTextExtractor : ITextExtractor
{
    public bool CanHandle(string fileName)
        => Path.GetExtension(fileName).Equals(".pdf", StringComparison.OrdinalIgnoreCase);

    public Task<string> ExtractAsync(Stream stream, string fileName, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        ms.Position = 0;

        using var pdf = PdfDocument.Open(ms);
        var sb = new StringBuilder();

        foreach (Page page in pdf.GetPages())
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var text = page.Text;
                if (!string.IsNullOrWhiteSpace(text))
                    sb.AppendLine(text);
            }
            catch
            {
                // Skip pages that can't be parsed (images, corrupt data, etc.)
            }
        }

        return Task.FromResult(sb.ToString());
    }

    public Task<IReadOnlyList<PagedTextSegment>> ExtractPagedAsync(Stream stream, string fileName, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        ms.Position = 0;

        using var pdf = PdfDocument.Open(ms);
        var segments = new List<PagedTextSegment>();
        int offset = 0;

        foreach (Page page in pdf.GetPages())
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var text = page.Text;
                if (!string.IsNullOrWhiteSpace(text))
                {
                    segments.Add(new PagedTextSegment(page.Number, offset, text));
                    offset += text.Length + 1;
                }
            }
            catch
            {
                // Skip pages that can't be parsed
            }
        }

        return Task.FromResult<IReadOnlyList<PagedTextSegment>>(segments);
    }
}
