using ResolveOps.Domain.Ai;

namespace ResolveOps.Modules.Ai.Services.Rag;

public sealed record KnowledgeSearchResult(
    Guid Id,
    Guid TenantId,
    AiKnowledgeType KnowledgeType,
    Guid? CarrierId,
    string Title,
    string ContentChunk,
    string MetadataJson,
    float SimilarityScore
);

public interface IAiKnowledgeRetrievalService
{
    Task<Guid> IngestKnowledgeAsync(
        Guid tenantId,
        AiKnowledgeType knowledgeType,
        Guid? carrierId,
        string title,
        string contentChunk,
        string metadataJson,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KnowledgeSearchResult>> SearchSimilarAsync(
        Guid tenantId,
        string query,
        int limit = 5,
        Guid? carrierId = null,
        CancellationToken cancellationToken = default);
}
