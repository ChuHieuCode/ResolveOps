using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ResolveOps.Application;
using ResolveOps.Domain.Ai;
using ResolveOps.Persistence;
using ResolveOps.Security;

namespace ResolveOps.Modules.Ai.Features.ReviewQueue.GetAiReviewQueue;

public sealed record AiTaskSummaryDto(
    Guid Id,
    Guid TenantId,
    string TaskType,
    string SourceEntityType,
    Guid SourceEntityId,
    string Status,
    string ModelName,
    decimal? Confidence,
    string? ReviewStatus,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CompletedAtUtc
);

public sealed record GetAiReviewQueueResponse(
    IReadOnlyList<AiTaskSummaryDto> Items,
    int TotalCount,
    int PageNumber,
    int PageSize
);

public sealed class GetAiReviewQueueHandler
{
    private readonly AppDbContext _dbContext;

    public GetAiReviewQueueHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<GetAiReviewQueueResponse> HandleAsync(
        Guid tenantId,
        AiTaskType? taskType,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
    {
        pageNumber = Math.Max(1, pageNumber);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = _dbContext.AiTasks
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId);

        if (taskType.HasValue)
        {
            query = query.Where(t => t.TaskType == taskType.Value);
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(t => t.CreatedAtUtc)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new AiTaskSummaryDto(
                t.Id,
                t.TenantId,
                t.TaskType.ToString(),
                t.SourceEntityType,
                t.SourceEntityId,
                t.Status.ToString(),
                t.ModelName,
                t.Confidence,
                t.ReviewStatus.HasValue ? t.ReviewStatus.Value.ToString() : null,
                t.CreatedAtUtc,
                t.CompletedAtUtc))
            .ToListAsync(cancellationToken);

        return new GetAiReviewQueueResponse(items, total, pageNumber, pageSize);
    }
}

public sealed class GetAiReviewQueueEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/ai/review-queue", async (
            AiTaskType? taskType,
            int? pageNumber,
            int? pageSize,
            GetAiReviewQueueHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var tenantId = httpContext.GetTenantId();
            var response = await handler.HandleAsync(
                tenantId,
                taskType,
                pageNumber ?? 1,
                pageSize ?? 20,
                cancellationToken);

            return Results.Ok(response);
        })
        .RequireAuthorization(AuthorizationPolicies.RequireActiveTenantMembership)
        .WithName("GetAiReviewQueue")
        .WithTags("AI");
    }
}
