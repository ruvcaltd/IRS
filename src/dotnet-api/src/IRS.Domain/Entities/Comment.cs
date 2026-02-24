using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IRS.Infrastructure;

[Index("section_id", "created_at", Name = "IX_Comments_SectionId_CreatedAt")]
public partial class Comment
{
    [Key]
    public int id { get; set; }

    public int section_id { get; set; }

    public int? author_id { get; set; }

    [StringLength(50)]
    public string? author_type { get; set; }

    [StringLength(100)]
    public string? author_agent_name { get; set; }

    public string content { get; set; } = null!;

    public DateTime? created_at { get; set; }

    public bool is_deleted { get; set; }

    public DateTime? deleted_at { get; set; }

    [ForeignKey("author_id")]
    [InverseProperty("Comments")]
    public virtual User? author { get; set; }

    [ForeignKey("section_id")]
    [InverseProperty("Comments")]
    public virtual Section section { get; set; } = null!;
}
