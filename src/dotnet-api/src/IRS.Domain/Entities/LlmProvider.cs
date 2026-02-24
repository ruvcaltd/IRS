using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IRS.Infrastructure;

public partial class LlmProvider
{
    [Key]
    public int id { get; set; }

    [StringLength(50)]
    public string name { get; set; } = null!;

    [StringLength(100)]
    public string display_name { get; set; } = null!;

    public bool is_active { get; set; }

    public DateTime? created_at { get; set; }

    public DateTime? updated_at { get; set; }

    [InverseProperty("provider")]
    public virtual ICollection<LlmModel> LlmModels { get; set; } = new List<LlmModel>();
}
