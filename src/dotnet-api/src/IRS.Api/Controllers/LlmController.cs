using IRS.Application.DTOs.Llm;
using IRS.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace IRS.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class LlmController : ControllerBase
{
    private readonly ILlmConfigurationService _llmConfigService;

    public LlmController(ILlmConfigurationService llmConfigService)
    {
        _llmConfigService = llmConfigService;
    }

    /// <summary>
    /// Get all available LLM providers
    /// </summary>
    [HttpGet("providers")]
    public async Task<ActionResult<List<LlmProviderResponse>>> GetProviders()
    {
        var providers = await _llmConfigService.GetProvidersAsync();
        return Ok(providers);
    }

    /// <summary>
    /// Get all available LLM models, optionally filtered by provider
    /// </summary>
    [HttpGet("models")]
    public async Task<ActionResult<List<LlmModelResponse>>> GetModels([FromQuery] int? providerId = null)
    {
        var models = await _llmConfigService.GetModelsAsync(providerId);
        return Ok(models);
    }

    /// <summary>
    /// Get global LLM configuration
    /// </summary>
    [HttpGet("global-config")]
    public async Task<ActionResult<GlobalLlmConfigResponse>> GetGlobalConfig()
    {
        var config = await _llmConfigService.GetGlobalConfigAsync();
        return Ok(config);
    }

    /// <summary>
    /// Update global LLM configuration
    /// </summary>
    [HttpPut("global-config")]
    public async Task<ActionResult> UpdateGlobalConfig([FromBody] UpdateGlobalLlmConfigRequest request)
    {
        // TODO: Get actual user ID from authentication context
        // For now, using a placeholder
        int userId = 1;

        await _llmConfigService.UpdateGlobalConfigAsync(request, userId);
        return NoContent();
    }

    /// <summary>
    /// Get LLM configuration for a specific agent
    /// </summary>
    [HttpGet("agents/{agentId}/config")]
    public async Task<ActionResult<AgentLlmConfigResponse>> GetAgentConfig(int agentId)
    {
        var config = await _llmConfigService.GetAgentConfigAsync(agentId);
        return Ok(config);
    }

    /// <summary>
    /// Update LLM configuration for a specific agent
    /// </summary>
    [HttpPut("agents/{agentId}/config")]
    public async Task<ActionResult> UpdateAgentConfig(int agentId, [FromBody] UpdateAgentLlmConfigRequest request)
    {
        await _llmConfigService.UpdateAgentConfigAsync(agentId, request);
        return NoContent();
    }
}
