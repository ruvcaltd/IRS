using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IRS.Infrastructure;

[Index("team_id", "security_figi", Name = "UQ_ResearchPages_Team_Security", IsUnique = true)]
public partial class ResearchPage
{
    [Key]
    public int id { get; set; }

    public int team_id { get; set; }

    [StringLength(50)]
    public string security_figi { get; set; } = null!;

    public int? conviction_score { get; set; }

    public int? fundamental_score { get; set; }

    public string? page_summary { get; set; }

    public DateTime? created_at { get; set; }

    public DateTime? last_updated { get; set; }

    public bool is_deleted { get; set; }

    public DateTime? deleted_at { get; set; }

    [InverseProperty("research_page")]
    public virtual ICollection<ResearchPageAgent> ResearchPageAgents { get; set; } = new List<ResearchPageAgent>();

    [InverseProperty("research_page")]
    public virtual ICollection<Section> Sections { get; set; } = new List<Section>();

    [ForeignKey("security_figi")]
    [InverseProperty("ResearchPages")]
    public virtual Security security_figiNavigation { get; set; } = null!;

    [ForeignKey("team_id")]
    [InverseProperty("ResearchPages")]
    public virtual Team team { get; set; } = null!;
}
