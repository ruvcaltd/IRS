using LlmTornado.Chat;

namespace IRS.LLM.Services;

/// <summary>
/// High-level service for LLM conversation interactions
/// </summary>
public interface ILlmConversationService
{
    /// <summary>
    /// Gets a completion for a simple system + user message
    /// </summary>
    /// <param name="agentId">Agent ID to use for LLM configuration</param>
    /// <param name="systemMessage">System message</param>
    /// <param name="userMessage">User message</param>
    /// <returns>LLM response text</returns>
    Task<string> GetCompletionAsync(int agentId, string systemMessage, string userMessage);

    /// <summary>
    /// Gets a completion for a list of messages
    /// </summary>
    /// <param name="agentId">Agent ID to use for LLM configuration</param>
    /// <param name="messages">List of chat messages</param>
    /// <returns>LLM response text</returns>
    Task<string> GetCompletionAsync(int agentId, List<ChatMessage> messages);

    /// <summary>
    /// Streams a completion with token-by-token callback
    /// </summary>
    /// <param name="agentId">Agent ID to use for LLM configuration</param>
    /// <param name="messages">List of chat messages</param>
    /// <param name="onToken">Callback for each token received</param>
    Task StreamCompletionAsync(int agentId, List<ChatMessage> messages, Action<string> onToken);

    /// <summary>
    /// Gets a completion using the global LLM configuration
    /// </summary>
    /// <param name="systemMessage">System message</param>
    /// <param name="userMessage">User message</param>
    /// <returns>LLM response text</returns>
    Task<string> GetGlobalCompletionAsync(string systemMessage, string userMessage);
}
