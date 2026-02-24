namespace IRS.Application.DTOs.Research;

public class SectionResponse
{
    public int id { get; set; }
    public string title { get; set; } = string.Empty;
    public int? fundamental_score { get; set; }
    public int? conviction_score { get; set; }
    public string? section_summary { get; set; }
    public string? ai_generated_content { get; set; }
}
