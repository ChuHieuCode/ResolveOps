using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ResolveOps.Application;
using ResolveOps.Modules.Ai.Services.Completion;
using ResolveOps.Modules.Ai.Services.Embeddings;
using ResolveOps.Modules.Ai.Services.Governance;
using ResolveOps.Modules.Ai.Services.Rag;
using ResolveOps.Modules.Ai.Services.Security;
using ResolveOps.Persistence;

namespace ResolveOps.Modules.Ai;

public static class AiModule
{
    public static IServiceCollection AddAiModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var assembly = typeof(AiModule).Assembly;

        // Register this assembly's EF configurations with AppDbContext
        AppDbContext.AddConfigurationAssembly(assembly);

        // Auto-register handlers & endpoints
        services.AddHandlersFromAssembly(assembly);
        services.AddEndpointsFromAssembly(assembly);

        // Options & Governance
        services.Configure<AiOptions>(configuration.GetSection(AiOptions.SectionName));
        services.AddScoped<IAiFeatureFlagService, AiFeatureFlagService>();

        // Security & PII Redaction
        services.AddSingleton<IPiiSanitizer, PiiSanitizer>();

        // RAG & Vector Embeddings
        services.AddScoped<IEmbeddingService, DeterministicMockEmbeddingProvider>();
        services.AddScoped<IAiKnowledgeRetrievalService, AiKnowledgeRetrievalService>();

        // LLM Completion Service
        services.AddScoped<IAiCompletionService, DeterministicMockAiCompletionService>();

        return services;
    }

    public static IEndpointRouteBuilder MapAiEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var assembly = typeof(AiModule).Assembly;
        endpoints.MapEndpointsFromAssembly(assembly);
        return endpoints;
    }
}
