using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ResolveOps.Application;
using ResolveOps.Domain.Ai;
using ResolveOps.Persistence;
using ResolveOps.Security;

namespace ResolveOps.Modules.Ai.Features.Feedback.CreateAiFeedback;

public sealed record CreateAiFeedbackRequest(
    Guid AiTaskId,
    string? FieldPath,
    string? OriginalValue,
    string? CorrectedValue,
    AiFeedbackType FeedbackType
);

public sealed record CreateAiFeedbackResponse(
    Guid Id,
    Guid AiTaskId,
    string FeedbackType,
    DateTimeOffset CreatedAtUtc
);

public sealed class CreateAiFeedbackHandler
{
    private readonly AppDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public CreateAiFeedbackHandler(AppDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<CreateAiFeedbackResponse?> HandleAsync(
        Guid tenantId,
        Guid reviewerId,
        CreateAiFeedbackRequest request,
        CancellationToken cancellationToken)
    {
        // Verify AI task exists and belongs to this tenant
        var taskExists = await _dbContext.AiTasks
            .AnyAsync(t => t.TenantId == tenantId && t.Id == request.AiTaskId, cancellationToken);

        if (!taskExists)
        {
            return null;
        }

        var now = _timeProvider.GetUtcNow();
        var feedback = AiFeedback.Create(
            tenantId: tenantId,
            aiTaskId: request.AiTaskId,
            fieldPath: request.FieldPath,
            originalValue: request.OriginalValue,
            correctedValue: request.CorrectedValue,
            feedbackType: request.FeedbackType,
            reviewerId: reviewerId,
            now: now);

        _dbContext.AiFeedbacks.Add(feedback);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CreateAiFeedbackResponse(
            feedback.Id,
            feedback.AiTaskId,
            feedback.FeedbackType.ToString(),
            feedback.CreatedAtUtc);
    }
}

public sealed class CreateAiFeedbackEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/ai/feedback", async (
            CreateAiFeedbackRequest request,
            CreateAiFeedbackHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var tenantId = httpContext.GetTenantId();
            var reviewerId = httpContext.GetUserId();

            var result = await handler.HandleAsync(tenantId, reviewerId, request, cancellationToken);

            return result is not null
                ? Results.Ok(result)
                : Results.NotFound(new { Code = "RESOURCE_NOT_FOUND", Message = $"AI Task with ID {request.AiTaskId} was not found." });
        })
        .RequireAuthorization(AuthorizationPolicies.RequireActiveTenantMembership)
        .WithName("CreateAiFeedback")
        .WithTags("AI");
    }
}
