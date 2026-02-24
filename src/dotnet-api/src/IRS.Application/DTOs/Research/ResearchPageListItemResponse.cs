using System;

namespace IRS.Application.DTOs.Research;

public class ResearchPageListItemResponse
{
    public int id { get; set; }
    public int team_id { get; set; }
    public string security_figi { get; set; } = string.Empty;
    public string? security_ticker { get; set; }
    public string? security_name { get; set; }
    public string? security_type { get; set; }
    public int? conviction_score { get; set; }
    public int? fundamental_score { get; set; }
    public DateTime? last_updated { get; set; }
}
