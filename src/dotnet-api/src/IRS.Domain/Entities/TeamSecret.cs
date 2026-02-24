using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IRS.Infrastructure;

[PrimaryKey("team_id", "key_name")]
public partial class TeamSecret
{
    [Key]
    public int team_id { get; set; }

    [Key]
    [StringLength(100)]
    public string key_name { get; set; } = null!;

    public byte[] encrypted_value { get; set; } = null!;

    public DateTime? created_at { get; set; }

    [ForeignKey("team_id")]
    [InverseProperty("TeamSecrets")]
    public virtual Team team { get; set; } = null!;
}
