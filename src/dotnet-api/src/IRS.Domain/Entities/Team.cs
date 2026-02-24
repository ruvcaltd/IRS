using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IRS.Infrastructure;

[Index("name", Name = "IX_Teams_Name")]
[Index("name", Name = "UQ__Teams__72E12F1B74B1FC16", IsUnique = true)]
public partial class Team
{
    [Key]
    public int id { get; set; }

    [StringLength(255)]
    public string name { get; set; } = null!;

    public DateTime? created_at { get; set; }

    public bool is_deleted { get; set; }

    public DateTime? deleted_at { get; set; }

    [InverseProperty("team")]
    public virtual ICollection<Agent> Agents { get; set; } = new List<Agent>();

    [InverseProperty("team")]
    public virtual ICollection<ResearchPage> ResearchPages { get; set; } = new List<ResearchPage>();

    [InverseProperty("team")]
    public virtual ICollection<TeamMember> TeamMembers { get; set; } = new List<TeamMember>();

    [InverseProperty("team")]
    public virtual ICollection<TeamSecret> TeamSecrets { get; set; } = new List<TeamSecret>();
}
