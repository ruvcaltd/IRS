using System.ComponentModel.DataAnnotations;

namespace IRS.Application.DTOs.Agents;

public class AttachAgentRequest
{
    [Required]
    public int agent_id { get; set; }
}
