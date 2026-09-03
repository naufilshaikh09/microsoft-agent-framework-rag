using System.Text;

namespace AgentFrameworkRag.Api.Services;

public static class TextChunker
{
    public static IReadOnlyList<string> Chunk(string text, int chunkSize = 800, int overlap = 150)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];

        var chunks = new List<string>();
        var paragraphs = text.Replace("\r\n", "\n")
            .Split(["\n\n"], StringSplitOptions.RemoveEmptyEntries);

        string overlapTail = string.Empty;

        foreach (var paragraph in paragraphs)
        {
            var trimmed = paragraph.Trim();
            if (trimmed.Length == 0) continue;

            // Prepend overlap from the previous chunk/paragraph
            var effective = overlapTail.Length > 0
                ? overlapTail + " " + trimmed
                : trimmed;

            if (effective.Length <= chunkSize)
            {
                chunks.Add(effective);
                overlapTail = TailOf(effective, overlap);
                continue;
            }

            // Paragraph too large — try sentence-level splits
            var sentences = SplitSentences(effective);
            var buffer = new StringBuilder();

            foreach (var sentence in sentences)
            {
                var s = sentence.Trim();
                if (s.Length == 0) continue;

                if (s.Length > chunkSize)
                {
                    // Single sentence exceeds limit — fall back to word-boundary char split
                    if (buffer.Length > 0)
                    {
                        chunks.Add(buffer.ToString().Trim());
                        overlapTail = TailOf(buffer.ToString().Trim(), overlap);
                        buffer.Clear();
                    }
                    var charChunks = SplitByChars(s, chunkSize, overlap).ToList();
                    chunks.AddRange(charChunks);
                    overlapTail = charChunks.Count > 0 ? TailOf(charChunks[^1], overlap) : string.Empty;
                }
                else if (buffer.Length > 0 && buffer.Length + 1 + s.Length > chunkSize)
                {
                    var completed = buffer.ToString().Trim();
                    chunks.Add(completed);
                    overlapTail = TailOf(completed, overlap);
                    buffer.Clear();
                    buffer.Append(overlapTail.Length > 0 ? overlapTail + " " + s : s);
                }
                else
                {
                    if (buffer.Length > 0) buffer.Append(' ');
                    buffer.Append(s);
                }
            }

            if (buffer.Length > 0)
            {
                var last = buffer.ToString().Trim();
                chunks.Add(last);
                overlapTail = TailOf(last, overlap);
            }
        }

        return chunks.Where(c => c.Length > 0).ToList();
    }

    public readonly record struct AnnotatedChunk(string Content, int PageNumber, int StartOffset);

    public static IReadOnlyList<AnnotatedChunk> ChunkPaged(
        IReadOnlyList<PagedTextSegment> segments,
        int chunkSize = 800,
        int overlap = 150)
    {
        if (segments.Count == 0) return [];

        var annotated = new List<AnnotatedChunk>();

        foreach (var segment in segments)
        {
            var rawChunks = Chunk(segment.Text, chunkSize, overlap);
            int offset = segment.StartOffset;

            foreach (var content in rawChunks)
            {
                annotated.Add(new AnnotatedChunk(content, segment.PageNumber, offset));
                offset += content.Length;
            }
        }

        return annotated;
    }

    // Returns the last `length` characters of `text`, trimmed to a word boundary
    private static string TailOf(string text, int length)
    {
        if (text.Length <= length) return text;
        var tail = text[^length..];
        var firstSpace = tail.IndexOf(' ');
        return firstSpace > 0 ? tail[firstSpace..].TrimStart() : tail;
    }

    private static List<string> SplitSentences(string text)
    {
        var result = new List<string>();
        int start = 0;

        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] is '.' or '?' or '!')
            {
                int j = i + 1;
                while (j < text.Length && char.IsWhiteSpace(text[j])) j++;

                // Only split if next non-whitespace is uppercase, quote, or end of text
                if (j == text.Length || char.IsUpper(text[j]) || text[j] is '"' or '\'')
                {
                    var sentence = text[start..(i + 1)].Trim();
                    if (sentence.Length > 0) result.Add(sentence);
                    start = j;
                    i = j - 1;
                }
            }
        }

        if (start < text.Length)
        {
            var last = text[start..].Trim();
            if (last.Length > 0) result.Add(last);
        }

        return result.Count > 0 ? result : [text];
    }

    private static IEnumerable<string> SplitByChars(string text, int chunkSize, int overlap)
    {
        int start = 0;
        while (start < text.Length)
        {
            int end = Math.Min(start + chunkSize, text.Length);
            if (end < text.Length && !char.IsWhiteSpace(text[end]))
            {
                int lastSpace = text.LastIndexOf(' ', end, end - start);
                if (lastSpace > start) end = lastSpace;
            }
            var chunk = text[start..end].Trim();
            if (chunk.Length > 0) yield return chunk;
            start += chunkSize - overlap;
        }
    }
}
