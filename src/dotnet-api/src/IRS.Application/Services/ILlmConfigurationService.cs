using IRS.Application.DTOs.Llm;

namespace IRS.Application.Services;

public interface ILlmConfigurationService
{
    Task<List<LlmProviderResponse>> GetProvidersAsync();
    Task<List<LlmModelResponse>> GetModelsAsync(int? providerId = null);
    Task<AgentLlmConfigResponse> GetAgentConfigAsync(int agentId);
    Task UpdateAgentConfigAsync(int agentId, UpdateAgentLlmConfigRequest request);
    Task<GlobalLlmConfigResponse> GetGlobalConfigAsync();
    Task UpdateGlobalConfigAsync(UpdateGlobalLlmConfigRequest request, int userId);
}
