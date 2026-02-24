using System;

namespace IRS.Application.DTOs.Agents;

public class PageAgentResponse
{
    public int id { get; set; } // ResearchPageAgent id
    public int agent_id { get; set; }
    public string name { get; set; } = string.Empty;
    public bool is_enabled { get; set; }
    public string? last_run_status { get; set; }
    public DateTime? last_run_at { get; set; }
}
