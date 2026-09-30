using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using ResolveOps.Application;
using ResolveOps.Modules.Ai.Services.Rag;
using ResolveOps.Security;

namespace ResolveOps.Modules.Ai.Features.Rag.SemanticSearch;

public sealed record SemanticSearchResponse(
    string Query,
    int ResultCount,
    IReadOnlyList<KnowledgeSearchResult> Matches
);

public sealed class SemanticSearchHandler
{
    private readonly IAiKnowledgeRetrievalService _retrievalService;

    public SemanticSearchHandler(IAiKnowledgeRetrievalService retrievalService)
    {
        _retrievalService = retrievalService;
    }

    public async Task<SemanticSearchResponse> HandleAsync(
        Guid tenantId,
        string query,
        int limit,
        Guid? carrierId,
        CancellationToken cancellationToken)
    {
        var matches = await _retrievalService.SearchSimilarAsync(
            tenantId,
            query,
            limit,
            carrierId,
            cancellationToken);

        return new SemanticSearchResponse(query, matches.Count, matches);
    }
}

public sealed class SemanticSearchEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet("/api/ai/semantic-search", async (
            string q,
            int? limit,
            Guid? carrierId,
            SemanticSearchHandler handler,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(q))
            {
                return Results.BadRequest(new { Code = "VALIDATION_FAILED", Message = "Query parameter 'q' must not be empty." });
            }

            var tenantId = httpContext.GetTenantId();
            var response = await handler.HandleAsync(
                tenantId,
                q,
                limit ?? 5,
                carrierId,
                cancellationToken);

            return Results.Ok(response);
        })
        .RequireAuthorization(AuthorizationPolicies.RequireActiveTenantMembership)
        .WithName("SemanticSearch")
        .WithTags("AI");
    }
}
