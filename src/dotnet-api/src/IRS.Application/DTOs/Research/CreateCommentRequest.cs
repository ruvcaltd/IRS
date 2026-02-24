using System.ComponentModel.DataAnnotations;

namespace IRS.Application.DTOs.Research;

public class CreateCommentRequest
{
    [Required]
    public string content { get; set; } = string.Empty;
}
