using LlmTornado;
using LlmTornado.Code;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using IRS.Infrastructure.Data;
using IRS.LLM.Services;

namespace IRS.Application.Services;

/// <summary>
/// Factory for creating LLM client instances
/// </summary>
public class LlmClientFactory : LLM.Services.ILlmClientFactory
{
    private readonly IrsDbContext _context;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<LlmClientFactory> _logger;

    public LlmClientFactory(
        IrsDbContext context,
        IEncryptionService encryptionService,
        ILogger<LlmClientFactory> logger)
    {
        _context = context;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    public TornadoApi CreateClient(LLM.Models.LlmConfiguration configuration)
    {
        var provider = MapProviderType(configuration.Provider);
        var api = new TornadoApi(provider, configuration.ApiKey);
        
        _logger.LogInformation("Created LLM client for provider {Provider} with model {Model}", 
            configuration.Provider, configuration.ModelIdentifier);
        
        return api;
    }

    public async Task<TornadoApi> CreateClientForAgentAsync(int agentId)
    {
        var agent = await _context.Agents
            .Include(a => a.llm_model)
            .ThenInclude(m => m!.provider)
            .FirstOrDefaultAsync(a => a.id == agentId);

        if (agent == null)
        {
            throw new InvalidOperationException($"Agent with ID {agentId} not found");
        }

        if (agent.llm_model_id == null || agent.llm_model == null)
        {
            throw new InvalidOperationException($"Agent {agentId} does not have an LLM model configured");
        }

        if (agent.encrypted_llm_api_key == null || agent.encrypted_llm_api_key.Length == 0)
        {
            throw new InvalidOperationException($"Agent {agentId} does not have an API key configured");
        }

        var apiKey = _encryptionService.Decrypt(agent.encrypted_llm_api_key);
        var providerType = (LLM.Models.LlmProviderType)agent.llm_model.provider_id;

        var configuration = new LLM.Models.LlmConfiguration
        {
            Provider = providerType,
            ModelIdentifier = agent.llm_model.model_identifier,
            ApiKey = apiKey,
            SupportsStreaming = agent.llm_model.supports_streaming,
            SupportsFunctionCalling = agent.llm_model.supports_function_calling,
            SupportsVision = agent.llm_model.supports_vision,
            MaxTokens = agent.llm_model.max_tokens,
            ContextWindow = agent.llm_model.context_window
        };

        return CreateClient(configuration);
    }

    public async Task<string> GetModelForAgentAsync(int agentId)
    {
        var agent = await _context.Agents
            .Include(a => a.llm_model)
            .FirstOrDefaultAsync(a => a.id == agentId);

        if (agent == null)
            throw new InvalidOperationException($"Agent with ID {agentId} not found");

        if (agent.llm_model_id == null || agent.llm_model == null)
            throw new InvalidOperationException($"Agent {agentId} does not have an LLM model configured");

        return agent.llm_model.model_identifier ?? string.Empty;
    }

    public async Task<TornadoApi> CreateGlobalClientAsync()
    {
        var globalConfig = await _context.GlobalLlmConfigurations
            .Include(g => g.global_model)
            .ThenInclude(m => m!.provider)
            .FirstOrDefaultAsync(g => g.id == 1);

        if (globalConfig == null || globalConfig.global_model_id == null || globalConfig.global_model == null)
        {
            throw new InvalidOperationException("Global LLM configuration is not set");
        }

        if (globalConfig.encrypted_global_api_key == null || globalConfig.encrypted_global_api_key.Length == 0)
        {
            throw new InvalidOperationException("Global LLM API key is not configured");
        }

        var apiKey = _encryptionService.Decrypt(globalConfig.encrypted_global_api_key);
        var providerType = (LLM.Models.LlmProviderType)globalConfig.global_model.provider_id;

        var configuration = new LLM.Models.LlmConfiguration
        {
            Provider = providerType,
            ModelIdentifier = globalConfig.global_model.model_identifier,
            ApiKey = apiKey,
            SupportsStreaming = globalConfig.global_model.supports_streaming,
            SupportsFunctionCalling = globalConfig.global_model.supports_function_calling,
            SupportsVision = globalConfig.global_model.supports_vision,
            MaxTokens = globalConfig.global_model.max_tokens,
            ContextWindow = globalConfig.global_model.context_window
        };

        return CreateClient(configuration);
    }

    public async Task<string> GetGlobalModelAsync()
    {
        var globalConfig = await _context.GlobalLlmConfigurations
            .Include(g => g.global_model)
            .FirstOrDefaultAsync(g => g.id == 1);

        if (globalConfig == null || globalConfig.global_model_id == null || globalConfig.global_model == null)
            throw new InvalidOperationException("Global LLM configuration is not set");

        return globalConfig.global_model.model_identifier ?? string.Empty;
    }

    private LLmProviders MapProviderType(LLM.Models.LlmProviderType providerType)
    {
        return providerType switch
        {
            LLM.Models.LlmProviderType.OpenAi => LLmProviders.OpenAi,
            LLM.Models.LlmProviderType.Anthropic => LLmProviders.Anthropic,
            LLM.Models.LlmProviderType.Google => LLmProviders.Google,
            LLM.Models.LlmProviderType.DeepSeek => LLmProviders.DeepSeek,
            LLM.Models.LlmProviderType.Mistral => LLmProviders.Mistral,
            LLM.Models.LlmProviderType.Cohere => LLmProviders.Cohere,
            LLM.Models.LlmProviderType.Groq => LLmProviders.Groq,
            LLM.Models.LlmProviderType.XAi => LLmProviders.XAi,
            _ => throw new ArgumentException($"Unsupported provider type: {providerType}", nameof(providerType))
        };
    }
}
