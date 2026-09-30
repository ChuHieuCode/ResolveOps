using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using ResolveOps.Application;
using ResolveOps.Domain.Ai;
using ResolveOps.Messaging;
using ResolveOps.Modules.Ai.Contracts;
using ResolveOps.Modules.Ai.Services.Completion;
using ResolveOps.Modules.Ai.Services.Governance;
using ResolveOps.Modules.Ai.Services.Rag;
using ResolveOps.Modules.Ai.Services.Security;
using ResolveOps.Persistence;
using ResolveOps.Security;

namespace ResolveOps.Modules.Ai.Features.Tasks.RequestAiTask;

public sealed record RequestAiTaskCommand(
    AiTaskType TaskType,
    string SourceEntityType,
    Guid SourceEntityId,
    string InputText,
    Guid? CarrierId = null,
    bool ExecuteSynchronously = false
);

public sealed record RequestAiTaskResponse(
    Guid TaskId,
    string Status,
    decimal? Confidence,
    string? OutputJson,
    bool PromptInjectionDetected
);

public sealed class RequestAiTaskHandler
{
    private readonly AppDbContext _dbContext;
    private readonly IAiCompletionService _aiCompletionService;
    private readonly IAiKnowledgeRetrievalService _ragService;
    private readonly IAiFeatureFlagService _featureFlagService;
    private readonly IOutboxWriter _outboxWriter;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RequestAiTaskHandler> _logger;

    public RequestAiTaskHandler(
        AppDbContext dbContext,
        IAiCompletionService aiCompletionService,
        IAiKnowledgeRetrievalService ragService,
        IAiFeatureFlagService featureFlagService,
        IOutboxWriter outboxWriter,
        TimeProvider timeProvider,
        ILogger<RequestAiTaskHandler> logger)
    {
        _dbContext = dbContext;
        _aiCompletionService = aiCompletionService;
        _ragService = ragService;
        _featureFlagService = featureFlagService;
        _outboxWriter = outboxWriter;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<RequestAiTaskResponse> HandleAsync(
        Guid tenantId,
        RequestAiTaskCommand command,
        CancellationToken cancellationToken)
    {
        // Check kill switch
        if (!_featureFlagService.IsAiEnabled(tenantId))
        {
            throw new InvalidOperationException("AI processing is disabled for this tenant.");
        }

        var now = _timeProvider.GetUtcNow();
        var inputHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(command.InputText))).ToLowerInvariant();

        var task = AiTask.Create(
            tenantId: tenantId,
            taskType: command.TaskType,
            sourceEntityType: command.SourceEntityType,
            sourceEntityId: command.SourceEntityId,
            inputHash: inputHash,
            now: now);

        _dbContext.AiTasks.Add(task);

        if (!command.ExecuteSynchronously)
        {
            // Transactional Outbox pattern (Spec Invariant 9)
            _outboxWriter.Write(
                new ResolveOps.Messaging.Events.AiTaskRequestedV1(
                    task.Id,
                    tenantId,
                    command.TaskType.ToString(),
                    command.SourceEntityType,
                    command.SourceEntityId,
                    command.InputText,
                    command.CarrierId),
                tenantId: tenantId,
                correlationId: task.Id.ToString("D"));

            await _dbContext.SaveChangesAsync(cancellationToken);

            return new RequestAiTaskResponse(task.Id, task.Status.ToString(), null, null, false);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        // Execute synchronously
        task.MarkProcessing();

        string? knowledgeContext = null;
        if (command.CarrierId.HasValue || command.TaskType == AiTaskType.DraftCommunication || command.TaskType == AiTaskType.EvidenceRecommendation)
        {
            var ragResults = await _ragService.SearchSimilarAsync(tenantId, command.InputText, limit: 3, carrierId: command.CarrierId, cancellationToken: cancellationToken);
            if (ragResults.Count > 0)
            {
                knowledgeContext = string.Join("\n---\n", ragResults.Select(r => $"[{r.KnowledgeType}] {r.Title}:\n{r.ContentChunk}"));
            }
        }

        var promptRequest = new AiPromptRequest(
            TenantId: tenantId,
            TaskType: command.TaskType,
            SystemPrompt: PromptInjectionShield.SystemSecurityDirective,
            UntrustedInput: PromptInjectionShield.WrapUntrustedData(command.InputText),
            KnowledgeContext: knowledgeContext);

        string rawJson;
        decimal confidence;
        string modelProvider;
        string modelName;
        string promptVersion;
        bool injectionDetected;

        switch (command.TaskType)
        {
            case AiTaskType.EmailClassification:
            case AiTaskType.NoteClassification:
                var classResult = await _aiCompletionService.CompleteStructuredAsync<EmailClassificationOutput>(promptRequest, cancellationToken);
                rawJson = classResult.RawJson;
                confidence = classResult.Confidence;
                modelProvider = classResult.ModelProvider;
                modelName = classResult.ModelName;
                promptVersion = classResult.PromptVersion;
                injectionDetected = classResult.PromptInjectionDetected;
                break;

            case AiTaskType.TimelineSummary:
                var timeResult = await _aiCompletionService.CompleteStructuredAsync<TimelineSummaryOutput>(promptRequest, cancellationToken);
                rawJson = timeResult.RawJson;
                confidence = timeResult.Confidence;
                modelProvider = timeResult.ModelProvider;
                modelName = timeResult.ModelName;
                promptVersion = timeResult.PromptVersion;
                injectionDetected = timeResult.PromptInjectionDetected;
                break;

            case AiTaskType.EvidenceRecommendation:
                var evidResult = await _aiCompletionService.CompleteStructuredAsync<EvidenceRecommendationOutput>(promptRequest, cancellationToken);
                rawJson = evidResult.RawJson;
                confidence = evidResult.Confidence;
                modelProvider = evidResult.ModelProvider;
                modelName = evidResult.ModelName;
                promptVersion = evidResult.PromptVersion;
                injectionDetected = evidResult.PromptInjectionDetected;
                break;

            case AiTaskType.DraftCommunication:
                var draftResult = await _aiCompletionService.CompleteStructuredAsync<DraftCommunicationOutput>(promptRequest, cancellationToken);
                rawJson = draftResult.RawJson;
                confidence = draftResult.Confidence;
                modelProvider = draftResult.ModelProvider;
                modelName = draftResult.ModelName;
                promptVersion = draftResult.PromptVersion;
                injectionDetected = draftResult.PromptInjectionDetected;
                break;

            default:
                throw new NotSupportedException($"Task type {command.TaskType} is not supported.");
        }

        var reviewStatus = ConfidencePolicyEngine.EvaluateReviewStatus(command.TaskType, confidence, isFinancialOrLegal: false);
        task.Complete(rawJson, confidence, modelProvider, modelName, promptVersion, reviewStatus, _timeProvider.GetUtcNow());
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new RequestAiTaskResponse(task.Id, task.Status.ToString(), confidence, rawJson, injectionDetected);
    }
}

public sealed class RequestAiTaskEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/ai/tasks/request", async (
            RequestAiTaskCommand command,
            RequestAiTaskHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(command.InputText))
            {
                return Results.BadRequest(new { Code = "VALIDATION_FAILED", Message = "InputText must not be empty." });
            }

            var tenantId = httpContext.GetTenantId();
            var response = await handler.HandleAsync(tenantId, command, cancellationToken);

            return Results.Ok(response);
        })
        .RequireAuthorization(AuthorizationPolicies.RequireActiveTenantMembership)
        .WithName("RequestAiTask")
        .WithTags("AI");
    }
}
