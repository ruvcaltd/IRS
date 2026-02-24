namespace IRS.Application.DTOs.Llm;

public class UpdateGlobalLlmConfigRequest
{
    public int? GlobalModelId { get; set; }
    public string? ApiKey { get; set; } // Will be encrypted before storage
}
