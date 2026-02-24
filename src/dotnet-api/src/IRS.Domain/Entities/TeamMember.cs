using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IRS.Infrastructure;

[PrimaryKey("user_id", "team_id")]
public partial class TeamMember
{
    [Key]
    public int user_id { get; set; }

    [Key]
    public int team_id { get; set; }

    public int team_role_id { get; set; }

    [StringLength(50)]
    public string status { get; set; } = null!;

    public int? approved_by_user_id { get; set; }

    public DateTime? approved_at { get; set; }

    public DateTime? created_at { get; set; }

    public bool is_deleted { get; set; }

    public DateTime? deleted_at { get; set; }

    [ForeignKey("approved_by_user_id")]
    [InverseProperty("TeamMemberapproved_by_users")]
    public virtual User? approved_by_user { get; set; }

    [ForeignKey("team_id")]
    [InverseProperty("TeamMembers")]
    public virtual Team team { get; set; } = null!;

    [ForeignKey("team_role_id")]
    [InverseProperty("TeamMembers")]
    public virtual TeamRole team_role { get; set; } = null!;

    [ForeignKey("user_id")]
    [InverseProperty("TeamMemberusers")]
    public virtual User user { get; set; } = null!;
}
