using SharpToken;

namespace AgentFrameworkRag.Api.Services;

public static class TokenEstimator
{
    // cl100k_base is the encoding used by gpt-4o and text-embedding-3-small
    private static readonly GptEncoding Encoding = GptEncoding.GetEncoding("cl100k_base");

    public static int Estimate(string text) => Encoding.CountTokens(text);
}
