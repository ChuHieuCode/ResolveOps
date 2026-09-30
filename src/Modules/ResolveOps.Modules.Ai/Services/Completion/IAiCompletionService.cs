using ResolveOps.Domain.Ai;

namespace ResolveOps.Modules.Ai.Services.Completion;

public sealed record AiPromptRequest(
    Guid TenantId,
    AiTaskType TaskType,
    string SystemPrompt,
    string UntrustedInput,
    string? KnowledgeContext = null,
    string? ModelName = null,
    string? PromptVersion = null
);

public sealed record AiCompletionResult<T>(
    T Output,
    string RawJson,
    decimal Confidence,
    string ModelProvider,
    string ModelName,
    string PromptVersion,
    string InputHash,
    bool PromptInjectionDetected = false
) where T : class;

public interface IAiCompletionService
{
    Task<AiCompletionResult<T>> CompleteStructuredAsync<T>(
        AiPromptRequest request,
        CancellationToken cancellationToken = default) where T : class;
}
