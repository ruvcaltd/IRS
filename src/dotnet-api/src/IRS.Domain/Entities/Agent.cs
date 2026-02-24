using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace IRS.Infrastructure;

public partial class Agent
{
    [Key]
    public int id { get; set; }

    public int team_id { get; set; }

    public int owner_user_id { get; set; }

    [StringLength(255)]
    public string name { get; set; } = null!;

    [StringLength(1000)]
    public string? description { get; set; }

    [StringLength(50)]
    public string visibility { get; set; } = null!;

    [StringLength(50)]
    public string? version { get; set; }

    public DateTime? created_at { get; set; }

    public DateTime? updated_at { get; set; }

    public bool is_deleted { get; set; }

    public DateTime? deleted_at { get; set; }

    [StringLength(2000)]
    public string endpoint_url { get; set; } = null!;

    [StringLength(20)]
    public string? http_method { get; set; }

    [StringLength(50)]
    public string? auth_type { get; set; }

    [StringLength(255)]
    public string? username { get; set; }

    public byte[]? password { get; set; }

    public byte[]? api_token { get; set; }

    [StringLength(2000)]
    public string? login_endpoint_url { get; set; }

    public string? request_body_template { get; set; }

    public string? agent_instructions { get; set; }

    public string? response_mapping { get; set; }

    public int? llm_model_id { get; set; }

    public byte[]? encrypted_llm_api_key { get; set; }

    [InverseProperty("agent")]
    public virtual ICollection<ResearchPageAgent> ResearchPageAgents { get; set; } = new List<ResearchPageAgent>();

    [InverseProperty("agent")]
    public virtual ICollection<SectionAgent> SectionAgents { get; set; } = new List<SectionAgent>();

    [ForeignKey("owner_user_id")]
    [InverseProperty("Agents")]
    public virtual User owner_user { get; set; } = null!;

    [ForeignKey("team_id")]
    [InverseProperty("Agents")]
    public virtual Team team { get; set; } = null!;

    [ForeignKey("llm_model_id")]
    [InverseProperty("Agents")]
    public virtual LlmModel? llm_model { get; set; }
}
