using LlmTornado;

namespace IRS.LLM.Services;

/// <summary>
/// Factory for creating LLM client instances
/// </summary>
public interface ILlmClientFactory
{
    /// <summary>
    /// Creates an LLM client for a specific agent
    /// </summary>
    /// <param name="agentId">Agent ID</param>
    /// <returns>Configured TornadoApi client for the agent</returns>
    Task<TornadoApi> CreateClientForAgentAsync(int agentId);

    /// <summary>
    /// Returns the configured model identifier for a specific agent (e.g. "gpt-4o").
    /// </summary>
    Task<string> GetModelForAgentAsync(int agentId);

    /// <summary>
    /// Creates an LLM client using the global configuration
    /// </summary>
    /// <returns>Configured TornadoApi client using global settings</returns>
    Task<TornadoApi> CreateGlobalClientAsync();

    /// <summary>
    /// Returns the configured global model identifier.
    /// </summary>
    Task<string> GetGlobalModelAsync();
}
