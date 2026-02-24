using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace IRS.Infrastructure;

public partial class GlobalLlmConfiguration
{
    [Key]
    public int id { get; set; }

    public int? global_model_id { get; set; }

    public byte[]? encrypted_global_api_key { get; set; }

    public DateTime? updated_at { get; set; }

    public int? updated_by_user_id { get; set; }

    [ForeignKey("global_model_id")]
    [InverseProperty("GlobalLlmConfiguration")]
    public virtual LlmModel? global_model { get; set; }

    [ForeignKey("updated_by_user_id")]
    [InverseProperty("GlobalLlmConfigurations")]
    public virtual User? updated_by_user { get; set; }
}
