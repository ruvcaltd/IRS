namespace IRS.Application.DTOs.Llm;

public class UpdateAgentLlmConfigRequest
{
    public int? LlmModelId { get; set; }
    public string? ApiKey { get; set; } // Will be encrypted before storage
}
