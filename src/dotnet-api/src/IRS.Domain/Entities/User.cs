using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IRS.Infrastructure;

[Index("email", Name = "IX_Users_Email")]
[Index("email", Name = "UQ__Users__AB6E61647D6A8E05", IsUnique = true)]
public partial class User
{
    [Key]
    public int id { get; set; }

    [StringLength(255)]
    public string email { get; set; } = null!;

    public string password_hash { get; set; } = null!;

    [StringLength(255)]
    public string? full_name { get; set; }

    [StringLength(500)]
    public string? avatar_url { get; set; }

    public int role_id { get; set; }

    public DateTime? created_at { get; set; }

    public bool is_deleted { get; set; }

    public DateTime? deleted_at { get; set; }

    [InverseProperty("owner_user")]
    public virtual ICollection<Agent> Agents { get; set; } = new List<Agent>();

    [InverseProperty("author")]
    public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();

    [InverseProperty("approved_by_user")]
    public virtual ICollection<TeamMember> TeamMemberapproved_by_users { get; set; } = new List<TeamMember>();

    [InverseProperty("user")]
    public virtual ICollection<TeamMember> TeamMemberusers { get; set; } = new List<TeamMember>();

    [InverseProperty("updated_by_user")]
    public virtual ICollection<GlobalLlmConfiguration> GlobalLlmConfigurations { get; set; } = new List<GlobalLlmConfiguration>();

    [ForeignKey("role_id")]
    [InverseProperty("Users")]
    public virtual Role role { get; set; } = null!;
}
