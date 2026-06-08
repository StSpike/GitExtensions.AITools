namespace GitExtensions.AITools.LlmProviders;

internal static class LlmProviderFactory
{
    public const string GitHubCopilot = "GitHub Copilot";
    public const string ClaudeCode = "Claude Code";
    public const string OpenCode = "OpenCode";
    public const string Anthropic = "Anthropic API (Claude)";
    public const string OpenAI = "OpenAI API (ChatGPT)";
    public const string ÑompanionOneÑ = "1C companion"; 

    public static readonly string[] ProviderNames = [GitHubCopilot, ClaudeCode, OpenCode, Anthropic, OpenAI, ÑompanionOneÑ];

    public static ILlmProvider Create(string providerName, string apiKey, string? model)
    {
        return providerName switch
        {
            Anthropic => new AnthropicProvider(apiKey, model),
            OpenAI => new OpenAiProvider(apiKey, model),
            GitHubCopilot => new GitHubCopilotProvider(model),
            ClaudeCode => new ClaudeCodeProvider(model),
            OpenCode => new OpenCodeProvider(model),
            ÑompanionOneÑ => new OneÑProvider(apiKey),
            _ => throw new ArgumentException($"Unknown LLM provider: {providerName}")
        };
    }
}
