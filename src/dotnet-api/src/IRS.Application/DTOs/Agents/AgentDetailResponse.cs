namespace IRS.Application.DTOs.Agents;

public class AgentDetailResponse
{
    public int id { get; set; }
    public string name { get; set; } = string.Empty;
    public string? description { get; set; }
    public string visibility { get; set; } = "Private";
    public string endpoint_url { get; set; } = string.Empty;
    public string http_method { get; set; } = "GET";
    public string auth_type { get; set; } = "None";
    public string? username { get; set; }
    public bool has_password { get; set; }
    public bool has_api_token { get; set; }
    public string? login_endpoint_url { get; set; }
    public string? request_body_template { get; set; }
    public string agent_instructions { get; set; } = string.Empty;
    public string? response_mapping { get; set; }
    public string? version { get; set; }
    public DateTime? created_at { get; set; }
    public DateTime? updated_at { get; set; }
    public int? llm_model_id { get; set; }
    public bool has_llm_api_key { get; set; }
}