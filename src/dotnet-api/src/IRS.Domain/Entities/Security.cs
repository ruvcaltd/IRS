using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IRS.Infrastructure;

public partial class Security
{
    [Key]
    [StringLength(50)]
    public string figi { get; set; } = null!;

    [StringLength(50)]
    public string? ticker { get; set; }

    [StringLength(255)]
    public string? name { get; set; }

    [StringLength(100)]
    public string? market_sector { get; set; }

    [StringLength(20)]
    public string? security_type { get; set; }

    [StringLength(50)]
    public string? exchange_code { get; set; }

    public DateTime? last_synced_at { get; set; }

    [StringLength(50)]
    public string? mic_code { get; set; }

    [StringLength(50)]
    public string? share_class_figi { get; set; }

    [StringLength(50)]
    public string? composite_figi { get; set; }

    [StringLength(50)]
    public string? security_type2 { get; set; }

    [StringLength(100)]
    public string? security_description { get; set; }

    [InverseProperty("security_figiNavigation")]
    public virtual ICollection<ResearchPage> ResearchPages { get; set; } = new List<ResearchPage>();
}
