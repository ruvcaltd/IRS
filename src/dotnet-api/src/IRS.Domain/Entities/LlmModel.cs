using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IRS.Infrastructure;

public partial class LlmModel
{
    [Key]
    public int id { get; set; }

    public int provider_id { get; set; }

    [StringLength(100)]
    public string model_identifier { get; set; } = null!;

    [StringLength(150)]
    public string display_name { get; set; } = null!;

    public bool is_active { get; set; }

    public bool supports_streaming { get; set; }

    public bool supports_function_calling { get; set; }

    public bool supports_vision { get; set; }

    public int? max_tokens { get; set; }

    public int? context_window { get; set; }

    public DateTime? created_at { get; set; }

    public DateTime? updated_at { get; set; }

    [ForeignKey("provider_id")]
    [InverseProperty("LlmModels")]
    public virtual LlmProvider provider { get; set; } = null!;

    [InverseProperty("llm_model")]
    public virtual ICollection<Agent> Agents { get; set; } = new List<Agent>();

    [InverseProperty("global_model")]
    public virtual GlobalLlmConfiguration? GlobalLlmConfiguration { get; set; }
}
