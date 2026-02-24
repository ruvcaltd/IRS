using IRS.Application.DTOs.Llm;
using IRS.Infrastructure.Data;
using IRS.LLM.Services;
using Microsoft.EntityFrameworkCore;

namespace IRS.Application.Services;

public class LlmConfigurationService : ILlmConfigurationService
{
    private readonly IrsDbContext _context;
    private readonly IEncryptionService _encryptionService;

    public LlmConfigurationService(
        IrsDbContext context,
        IEncryptionService encryptionService)
    {
        _context = context;
        _encryptionService = encryptionService;
    }

    public async Task<List<LlmProviderResponse>> GetProvidersAsync()
    {
        return await _context.LlmProviders
            .Where(p => p.is_active)
            .OrderBy(p => p.display_name)
            .Select(p => new LlmProviderResponse
            {
                Id = p.id,
                Name = p.name,
                DisplayName = p.display_name,
                IsActive = p.is_active
            })
            .ToListAsync();
    }

    public async Task<List<LlmModelResponse>> GetModelsAsync(int? providerId = null)
    {
        var query = _context.LlmModels
            .Include(m => m.provider)
            .Where(m => m.is_active);

        if (providerId.HasValue)
        {
            query = query.Where(m => m.provider_id == providerId.Value);
        }

        return await query
            .OrderBy(m => m.provider.display_name)
            .ThenBy(m => m.display_name)
            .Select(m => new LlmModelResponse
            {
                Id = m.id,
                ProviderId = m.provider_id,
                ProviderName = m.provider.display_name,
                ModelIdentifier = m.model_identifier,
                DisplayName = m.display_name,
                IsActive = m.is_active,
                SupportsStreaming = m.supports_streaming,
                SupportsFunctionCalling = m.supports_function_calling,
                SupportsVision = m.supports_vision,
                MaxTokens = m.max_tokens,
                ContextWindow = m.context_window
            })
            .ToListAsync();
    }

    public async Task<AgentLlmConfigResponse> GetAgentConfigAsync(int agentId)
    {
        var agent = await _context.Agents
            .Include(a => a.llm_model)
            .ThenInclude(m => m!.provider)
            .FirstOrDefaultAsync(a => a.id == agentId);

        if (agent == null)
        {
            throw new InvalidOperationException($"Agent with ID {agentId} not found");
        }

        return new AgentLlmConfigResponse
        {
            AgentId = agent.id,
            LlmModelId = agent.llm_model_id,
            ModelDisplayName = agent.llm_model?.display_name,
            ProviderName = agent.llm_model?.provider?.display_name,
            HasApiKey = agent.encrypted_llm_api_key != null && agent.encrypted_llm_api_key.Length > 0
        };
    }

    public async Task UpdateAgentConfigAsync(int agentId, UpdateAgentLlmConfigRequest request)
    {
        var agent = await _context.Agents.FirstOrDefaultAsync(a => a.id == agentId);

        if (agent == null)
        {
            throw new InvalidOperationException($"Agent with ID {agentId} not found");
        }

        // Update model selection
        if (request.LlmModelId.HasValue)
        {
            var modelExists = await _context.LlmModels
                .AnyAsync(m => m.id == request.LlmModelId.Value && m.is_active);

            if (!modelExists)
            {
                throw new InvalidOperationException($"LLM Model with ID {request.LlmModelId.Value} not found or inactive");
            }

            agent.llm_model_id = request.LlmModelId.Value;
        }

        // Update API key if provided
        if (!string.IsNullOrWhiteSpace(request.ApiKey))
        {
            agent.encrypted_llm_api_key = _encryptionService.Encrypt(request.ApiKey);
        }

        agent.updated_at = DateTime.UtcNow;
        await _context.SaveChangesAsync();
    }

    public async Task<GlobalLlmConfigResponse> GetGlobalConfigAsync()
    {
        var config = await _context.GlobalLlmConfigurations
            .Include(g => g.global_model)
            .ThenInclude(m => m!.provider)
            .FirstOrDefaultAsync(g => g.id == 1);

        if (config == null)
        {
            // Return empty config if not set
            return new GlobalLlmConfigResponse
            {
                GlobalModelId = null,
                ModelDisplayName = null,
                ProviderName = null,
                HasApiKey = false,
                UpdatedAt = null,
                UpdatedByUserId = null
            };
        }

        return new GlobalLlmConfigResponse
        {
            GlobalModelId = config.global_model_id,
            ModelDisplayName = config.global_model?.display_name,
            ProviderName = config.global_model?.provider?.display_name,
            HasApiKey = config.encrypted_global_api_key != null && config.encrypted_global_api_key.Length > 0,
            UpdatedAt = config.updated_at,
            UpdatedByUserId = config.updated_by_user_id
        };
    }

    public async Task UpdateGlobalConfigAsync(UpdateGlobalLlmConfigRequest request, int userId)
    {
        var config = await _context.GlobalLlmConfigurations.FirstOrDefaultAsync(g => g.id == 1);

        if (config == null)
        {
            // Create initial config
            config = new Infrastructure.GlobalLlmConfiguration
            {
                id = 1
            };
            _context.GlobalLlmConfigurations.Add(config);
        }

        // Update model selection
        if (request.GlobalModelId.HasValue)
        {
            var modelExists = await _context.LlmModels
                .AnyAsync(m => m.id == request.GlobalModelId.Value && m.is_active);

            if (!modelExists)
            {
                throw new InvalidOperationException($"LLM Model with ID {request.GlobalModelId.Value} not found or inactive");
            }

            config.global_model_id = request.GlobalModelId.Value;
        }

        // Update API key if provided
        if (!string.IsNullOrWhiteSpace(request.ApiKey))
        {
            config.encrypted_global_api_key = _encryptionService.Encrypt(request.ApiKey);
        }

        config.updated_at = DateTime.UtcNow;
        config.updated_by_user_id = userId;

        await _context.SaveChangesAsync();
    }
}
