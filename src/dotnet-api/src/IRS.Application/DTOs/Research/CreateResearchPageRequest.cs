using System.ComponentModel.DataAnnotations;

namespace IRS.Application.DTOs.Research;

public class CreateResearchPageRequest
{
    [Required]
    public int team_id { get; set; }

    [Required]
    [StringLength(50)]
    public string figi { get; set; } = string.Empty;

    [StringLength(50)]
    public string? ticker { get; set; }

    [StringLength(255)]
    public string? name { get; set; }

    [Required]
    [StringLength(20)]
    public string security_type { get; set; } = string.Empty; // "Sovereign" or "Corporate"
}
