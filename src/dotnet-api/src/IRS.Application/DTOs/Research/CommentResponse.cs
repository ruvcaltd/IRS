using System;

namespace IRS.Application.DTOs.Research;

public class CommentResponse
{
    public int id { get; set; }
    public int section_id { get; set; }
    public int? author_id { get; set; }
    public string? author_type { get; set; }
    public string? author_agent_name { get; set; }
    public string content { get; set; } = string.Empty;
    public DateTime? created_at { get; set; }
}
