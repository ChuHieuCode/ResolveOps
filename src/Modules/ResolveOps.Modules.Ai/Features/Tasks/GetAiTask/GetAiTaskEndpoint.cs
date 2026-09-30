using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using ResolveOps.Application;
using ResolveOps.Persistence;
using ResolveOps.Security;

namespace ResolveOps.Modules.Ai.Features.Tasks.GetAiTask;

public sealed record AiTaskDetailDto(
    Guid Id,
    Guid TenantId,
    string TaskType,
    string SourceEntityType,
    Guid SourceEntityId,
    string Status,
    string ModelProvider,
    string ModelName,
    string PromptVersion,
    string InputHash,
    string? OutputJson,
    decimal? Confidence,
    string? FailureCode,
    string? ReviewStatus,
    Guid? ReviewedBy,
    DateTimeOffset? ReviewedAtUtc,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? CompletedAtUtc
);

public sealed class GetAiTaskHandler
{
    private readonly AppDbContext _dbContext;

    public GetAiTaskHandler(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<AiTaskDetailDto?> HandleAsync(
        Guid tenantId,
        Guid taskId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.AiTasks
            .AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.Id == taskId)
            .Select(t => new AiTaskDetailDto(
                t.Id,
                t.TenantId,
                t.TaskType.ToString(),
                t.SourceEntityType,
                t.SourceEntityId,
                t.Status.ToString(),
                t.ModelProvider,
                t.ModelName,
                t.PromptVersion,
                t.InputHash,
                t.OutputJson,
                t.Confidence,
                t.FailureCode,
                t.ReviewStatus.HasValue ? t.ReviewStatus.Value.ToString() : null,
                t.ReviewedBy,
                t.ReviewedAtUtc,
                t.CreatedAtUtc,
                t.CompletedAtUtc))
            .FirstOrDefaultAsync(cancellationToken);
    }
}

public sealed class GetAiTaskEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/ai/tasks/{id:guid}", async (
            Guid id,
            GetAiTaskHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            var tenantId = httpContext.GetTenantId();
            var task = await handler.HandleAsync(tenantId, id, cancellationToken);

            return task is not null
                ? Results.Ok(task)
                : Results.NotFound(new { Code = "RESOURCE_NOT_FOUND", Message = $"AI Task with ID {id} was not found." });
        })
        .RequireAuthorization(AuthorizationPolicies.RequireActiveTenantMembership)
        .WithName("GetAiTask")
        .WithTags("AI");
    }
}
