using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IRS.Infrastructure;

[Index("research_page_id", Name = "IX_ResearchPageAgents_ResearchPageId")]
[Index("research_page_id", "agent_id", Name = "UQ__Research__BF3183C453CCFBD7", IsUnique = true)]
public partial class ResearchPageAgent
{
    [Key]
    public int id { get; set; }

    public int research_page_id { get; set; }

    public int agent_id { get; set; }

    public bool is_enabled { get; set; }

    [StringLength(50)]
    public string? last_run_status { get; set; }

    public DateTime? last_run_at { get; set; }

    [InverseProperty("research_page_agent")]
    public virtual ICollection<AgentRun> AgentRuns { get; set; } = new List<AgentRun>();

    [ForeignKey("agent_id")]
    [InverseProperty("ResearchPageAgents")]
    public virtual Agent agent { get; set; } = null!;

    [ForeignKey("research_page_id")]
    [InverseProperty("ResearchPageAgents")]
    public virtual ResearchPage research_page { get; set; } = null!;
}
