namespace IRS.Application.DTOs.Llm;

public class LlmProviderResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}
