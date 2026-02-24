using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IRS.Infrastructure;

public partial class Section
{
    [Key]
    public int id { get; set; }

    public int research_page_id { get; set; }

    [StringLength(255)]
    public string title { get; set; } = null!;

    public int? fundamental_score { get; set; }

    public int? conviction_score { get; set; }

    public string? section_summary { get; set; }

    public string? ai_generated_content { get; set; }

    public bool is_deleted { get; set; }

    public DateTime? deleted_at { get; set; }

    [InverseProperty("section")]
    public virtual ICollection<AgentRun> AgentRuns { get; set; } = new List<AgentRun>();

    [InverseProperty("section")]
    public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();

    [InverseProperty("section")]
    public virtual ICollection<SectionAgent> SectionAgents { get; set; } = new List<SectionAgent>();

    [ForeignKey("research_page_id")]
    [InverseProperty("Sections")]
    public virtual ResearchPage research_page { get; set; } = null!;
}
