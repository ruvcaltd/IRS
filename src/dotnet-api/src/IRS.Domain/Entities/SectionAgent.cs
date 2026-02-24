using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IRS.Infrastructure;

[Index("section_id", Name = "IX_SectionAgents_SectionId")]
[Index("section_id", "agent_id", Name = "UQ__Section__BF3183C453CCFBD7", IsUnique = true)]
public partial class SectionAgent
{
    [Key]
    public int id { get; set; }

    public int section_id { get; set; }

    public int agent_id { get; set; }

    public bool is_enabled { get; set; }

    [StringLength(50)]
    public string? last_run_status { get; set; }

    public DateTime? last_run_at { get; set; }

    [InverseProperty("section_agent")]
    public virtual ICollection<AgentRun> AgentRuns { get; set; } = new List<AgentRun>();

    [ForeignKey("agent_id")]
    [InverseProperty("SectionAgents")]
    public virtual Agent agent { get; set; } = null!;

    [ForeignKey("section_id")]
    [InverseProperty("SectionAgents")]
    public virtual Section section { get; set; } = null!;
}