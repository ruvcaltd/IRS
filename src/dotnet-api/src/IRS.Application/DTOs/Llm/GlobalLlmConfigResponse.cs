namespace IRS.Application.DTOs.Llm;

public class GlobalLlmConfigResponse
{
    public int? GlobalModelId { get; set; }
    public string? ModelDisplayName { get; set; }
    public string? ProviderName { get; set; }
    public bool HasApiKey { get; set; } // Don't expose the actual key
    public DateTime? UpdatedAt { get; set; }
    public int? UpdatedByUserId { get; set; }
}
