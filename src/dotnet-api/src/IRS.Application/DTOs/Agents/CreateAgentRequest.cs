using System.ComponentModel.DataAnnotations;

namespace IRS.Application.DTOs.Agents;

public class CreateAgentRequest
{
    [Required]
    public string name { get; set; } = string.Empty;

    public string? description { get; set; }

    [Required]
    [RegularExpression("Private|Team")]
    public string visibility { get; set; } = "Private";

    [Required]
    [Url]
    public string endpoint_url { get; set; } = string.Empty;

    [Required]
    [RegularExpression("GET|POST|PUT|PATCH|DELETE")]
    public string http_method { get; set; } = "GET";

    [Required]
    [RegularExpression("None|BasicAuth|ApiToken|UsernamePassword")]
    public string auth_type { get; set; } = "None";

    public string? username { get; set; }

    public string? password { get; set; } // Plain text, will be encrypted on server

    public string? api_token { get; set; } // Plain text, will be encrypted on server

    [Url]
    public string? login_endpoint_url { get; set; } // For UsernamePassword auth: endpoint to obtain token

    public string? request_body_template { get; set; }

    [Required]
    public string agent_instructions { get; set; } = string.Empty;

    public string? response_mapping { get; set; }

    public string? version { get; set; }

    public int? llm_model_id { get; set; }

    public string? llm_api_key { get; set; } // Plain text, will be encrypted on server
}