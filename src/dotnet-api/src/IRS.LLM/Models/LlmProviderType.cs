namespace IRS.LLM.Models;

/// <summary>
/// LLM provider types supported by the system
/// Maps to LLMTornado's LLmProviders enum
/// </summary>
public enum LlmProviderType
{
    OpenAi = 1,
    Anthropic = 2,
    Google = 3,
    DeepSeek = 4,
    Mistral = 5,
    Cohere = 6,
    Groq = 7,
    XAi = 8
}
