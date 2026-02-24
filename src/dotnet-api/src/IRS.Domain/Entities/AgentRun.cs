using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IRS.Infrastructure;

[Index("status", "started_at", Name = "IX_AgentRuns_Status_StartedAt")]
[Index("section_agent_id", Name = "IX_AgentRuns_SectionAgentId")]
public partial class AgentRun
{
    [Key]
    public int id { get; set; }

    public int? research_page_agent_id { get; set; }

    public int? section_agent_id { get; set; }

    public int? section_id { get; set; }

    [StringLength(50)]
    public string status { get; set; } = null!;

    public DateTime? started_at { get; set; }

    public DateTime? completed_at { get; set; }

    public string? output { get; set; }

    public string? error { get; set; }

    [ForeignKey("research_page_agent_id")]
    [InverseProperty("AgentRuns")]
    public virtual ResearchPageAgent? research_page_agent { get; set; }

    [ForeignKey("section_agent_id")]
    [InverseProperty("AgentRuns")]
    public virtual SectionAgent? section_agent { get; set; }

    [ForeignKey("section_id")]
    [InverseProperty("AgentRuns")]
    public virtual Section? section { get; set; }
}
