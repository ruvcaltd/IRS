namespace IRS.Application.DTOs.Agents;

public class AvailableAgentResponse
{
    public int id { get; set; }
    public string name { get; set; } = string.Empty;
    public string visibility { get; set; } = string.Empty; // Private or Team
    public string endpoint_url { get; set; } = string.Empty;
    public string http_method { get; set; } = string.Empty;
    public string? description { get; set; }
}
