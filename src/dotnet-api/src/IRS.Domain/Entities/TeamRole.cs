using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IRS.Infrastructure;

[Index("name", Name = "UQ__TeamRole__72E12F1B07F72173", IsUnique = true)]
public partial class TeamRole
{
    [Key]
    public int id { get; set; }

    [StringLength(50)]
    public string name { get; set; } = null!;

    [InverseProperty("team_role")]
    public virtual ICollection<TeamMember> TeamMembers { get; set; } = new List<TeamMember>();
}
