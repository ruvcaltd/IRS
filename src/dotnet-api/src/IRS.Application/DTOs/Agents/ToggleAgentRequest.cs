using System.ComponentModel.DataAnnotations;

namespace IRS.Application.DTOs.Agents;

public class ToggleAgentRequest
{
    [Required]
    public bool is_enabled { get; set; }
}
