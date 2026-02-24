namespace IRS.Application.DTOs.Llm;

public class LlmModelResponse
{
    public int Id { get; set; }
    public int ProviderId { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string ModelIdentifier { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool SupportsStreaming { get; set; }
    public bool SupportsFunctionCalling { get; set; }
    public bool SupportsVision { get; set; }
    public int? MaxTokens { get; set; }
    public int? ContextWindow { get; set; }
}
