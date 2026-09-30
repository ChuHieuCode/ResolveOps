using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ResolveOps.Application;
using ResolveOps.Domain.Ai;
using ResolveOps.Persistence;
using ResolveOps.Security;

namespace ResolveOps.Modules.Ai.Features.Tasks.ReviewAiTask;

public sealed record ReviewAiTaskRequest(
    AiReviewStatus ReviewStatus,
    string? Notes
);

public sealed record ReviewAiTaskResponse(
    Guid Id,
    string ReviewStatus,
    Guid ReviewedBy,
    DateTimeOffset ReviewedAtUtc
);

public sealed class ReviewAiTaskHandler
{
    private readonly AppDbContext _dbContext;
    private readonly TimeProvider _timeProvider;

    public ReviewAiTaskHandler(AppDbContext dbContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _timeProvider = timeProvider;
    }

    public async Task<ReviewAiTaskResponse?> HandleAsync(
        Guid tenantId,
        Guid taskId,
        Guid reviewerId,
        ReviewAiTaskRequest request,
        CancellationToken cancellationToken)
    {
        var task = await _dbContext.AiTasks
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == taskId, cancellationToken);

        if (task is null)
        {
            return null;
        }

        var now = _timeProvider.GetUtcNow();
        task.Review(request.ReviewStatus, reviewerId, now);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ReviewAiTaskResponse(
            task.Id,
            task.ReviewStatus.ToString()!,
            reviewerId,
            now);
    }
}

public sealed class ReviewAiTaskEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/ai/tasks/{id:guid}/review", async (
            Guid id,
            ReviewAiTaskRequest request,
            ReviewAiTaskHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var tenantId = httpContext.GetTenantId();
            var reviewerId = httpContext.GetUserId();

            var result = await handler.HandleAsync(tenantId, id, reviewerId, request, cancellationToken);

            return result is not null
                ? Results.Ok(result)
                : Results.NotFound(new { Code = "RESOURCE_NOT_FOUND", Message = $"AI Task with ID {id} was not found." });
        })
        .RequireAuthorization(AuthorizationPolicies.RequireActiveTenantMembership)
        .WithName("ReviewAiTask")
        .WithTags("AI");
    }
}
