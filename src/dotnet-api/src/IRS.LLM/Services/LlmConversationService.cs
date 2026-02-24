using LlmTornado.Chat;
using LlmTornado.Chat.Models;
using LlmTornado.Code;
using Microsoft.Extensions.Logging;

namespace IRS.LLM.Services;

/// <summary>
/// High-level service for LLM conversation interactions
/// </summary>
public class LlmConversationService : ILlmConversationService
{
    private readonly ILlmClientFactory _clientFactory;
    private readonly ILogger<LlmConversationService> _logger;

    public LlmConversationService(
        ILlmClientFactory clientFactory,
        ILogger<LlmConversationService> logger)
    {
        _clientFactory = clientFactory;
        _logger = logger;
    }

    public async Task<string> GetCompletionAsync(int agentId, string systemMessage, string userMessage)
    {
        var messages = new List<ChatMessage>
        {
            new ChatMessage(ChatMessageRoles.System, systemMessage),
            new ChatMessage(ChatMessageRoles.User, userMessage)
        };

        return await GetCompletionAsync(agentId, messages);
    }

    public async Task<string> GetCompletionAsync(int agentId, List<ChatMessage> messages)
    {
        try
        {
            var client = await _clientFactory.CreateClientForAgentAsync(agentId);

            var model = await _clientFactory.GetModelForAgentAsync(agentId);

            var chatRequest = new ChatRequest
            {
                Model = model,
                Messages = messages
            };

            var result = await client.Chat.CreateChatCompletion(chatRequest);

            var response = result?.Choices?[0].Message?.Content ?? string.Empty;

            _logger.LogInformation("Completed LLM request for agent {AgentId}, response length: {Length}",
                agentId, response.Length);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting completion for agent {AgentId}", agentId);
            throw;
        }
    }

    public async Task StreamCompletionAsync(int agentId, List<ChatMessage> messages, Action<string> onToken)
    {
        try
        {
            var client = await _clientFactory.CreateClientForAgentAsync(agentId);

            var model = await _clientFactory.GetModelForAgentAsync(agentId);

            var conversation = client.Chat.CreateConversation(new ChatRequest
            {
                Model = model,
                Messages = messages
            });

            await conversation.StreamResponse(onToken);

            _logger.LogInformation("Completed streaming LLM request for agent {AgentId}", agentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error streaming completion for agent {AgentId}", agentId);
            throw;
        }
    }

    public async Task<string> GetGlobalCompletionAsync(string systemMessage, string userMessage)
    {
        try
        {
            var client = await _clientFactory.CreateGlobalClientAsync();

            var messages = new List<ChatMessage>
            {
                new ChatMessage(ChatMessageRoles.System, systemMessage),
                new ChatMessage(ChatMessageRoles.User, userMessage)
            };

            var model = await _clientFactory.GetGlobalModelAsync();

            var chatRequest = new ChatRequest
            {
                Model = model,
                Messages = messages
            };

            var result = await client.Chat.CreateChatCompletion(chatRequest);

            var response = result?.Choices?[0].Message?.Content ?? string.Empty;

            _logger.LogInformation("Completed global LLM request, response length: {Length}", response.Length);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting global completion");
            throw;
        }
    }
}
