namespace IRS.LLM.Models;

/// <summary>
/// Configuration for initializing an LLM client
/// </summary>
public class LlmConfiguration
{
    public LlmProviderType Provider { get; set; }
    public string ModelIdentifier { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public bool SupportsStreaming { get; set; }
    public bool SupportsFunctionCalling { get; set; }
    public bool SupportsVision { get; set; }
    public int? MaxTokens { get; set; }
    public int? ContextWindow { get; set; }
}
