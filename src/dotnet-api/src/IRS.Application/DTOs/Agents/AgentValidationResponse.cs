namespace IRS.Application.DTOs.Agents;

public class AgentValidationResponse
{
    public bool is_valid { get; set; }
    public string? log { get; set; }
}