using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using IRS.LLM.Services;

namespace IRS.LLM.Extensions;

/// <summary>
/// Extension methods for registering LLM services
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds LLM services to the dependency injection container
    /// </summary>
    /// <param name="services">Service collection</param>
    /// <param name="configuration">Configuration</param>
    /// <returns>Service collection for chaining</returns>
    public static IServiceCollection AddLlmServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IEncryptionService, EncryptionService>();
        services.AddScoped<Services.ILlmConversationService, Services.LlmConversationService>();
        
        return services;
    }
}
