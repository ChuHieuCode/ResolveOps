using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ResolveOps.Application;
using ResolveOps.Domain.Ai;
using ResolveOps.Modules.Ai.Services.Rag;
using ResolveOps.Security;

namespace ResolveOps.Modules.Ai.Features.Rag.IngestKnowledge;

public sealed record IngestKnowledgeRequest(
    AiKnowledgeType KnowledgeType,
    Guid? CarrierId,
    string Title,
    string ContentChunk,
    string? MetadataJson
);

public sealed record IngestKnowledgeResponse(
    Guid Id,
    string Title,
    string KnowledgeType
);

public sealed class IngestKnowledgeHandler
{
    private readonly IAiKnowledgeRetrievalService _retrievalService;

    public IngestKnowledgeHandler(IAiKnowledgeRetrievalService retrievalService)
    {
        _retrievalService = retrievalService;
    }

    public async Task<IngestKnowledgeResponse> HandleAsync(
        Guid tenantId,
        IngestKnowledgeRequest request,
        CancellationToken cancellationToken)
    {
        var id = await _retrievalService.IngestKnowledgeAsync(
            tenantId,
            request.KnowledgeType,
            request.CarrierId,
            request.Title,
            request.ContentChunk,
            request.MetadataJson ?? "{}",
            cancellationToken);

        return new IngestKnowledgeResponse(id, request.Title, request.KnowledgeType.ToString());
    }
}

public sealed class IngestKnowledgeEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost("/api/ai/knowledge", async (
            IngestKnowledgeRequest request,
            IngestKnowledgeHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.ContentChunk))
            {
                return Results.BadRequest(new { Code = "VALIDATION_FAILED", Message = "Title and ContentChunk are required." });
            }

            var tenantId = httpContext.GetTenantId();
            var response = await handler.HandleAsync(tenantId, request, cancellationToken);

            return Results.Ok(response);
        })
        .RequireAuthorization(AuthorizationPolicies.RequireActiveTenantMembership)
        .WithName("IngestKnowledge")
        .WithTags("AI");
    }
}
