using System;

namespace IRS.Application.DTOs.Agents;

public class AgentRunResponse
{
    public int id { get; set; }
    public int? research_page_agent_id { get; set; }
    public int? section_agent_id { get; set; }
    public int? section_id { get; set; }
    public string status { get; set; } = string.Empty;
    public DateTime? started_at { get; set; }
    public DateTime? completed_at { get; set; }
    public string? output { get; set; }
    public string? error { get; set; }
}
