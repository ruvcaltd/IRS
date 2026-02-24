namespace IRS.Application.DTOs.Llm;

public class AgentLlmConfigResponse
{
    public int AgentId { get; set; }
    public int? LlmModelId { get; set; }
    public string? ModelDisplayName { get; set; }
    public string? ProviderName { get; set; }
    public bool HasApiKey { get; set; } // Don't expose the actual key
}
