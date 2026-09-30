using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ResolveOps.Domain.Ai;
using ResolveOps.Modules.Ai.Services.Embeddings;
using ResolveOps.Persistence;

namespace ResolveOps.Modules.Ai.Services.Rag;

public sealed class AiKnowledgeRetrievalService : IAiKnowledgeRetrievalService
{
    private readonly AppDbContext _dbContext;
    private readonly IEmbeddingService _embeddingService;
    private readonly TimeProvider _timeProvider;

    public AiKnowledgeRetrievalService(
        AppDbContext dbContext,
        IEmbeddingService embeddingService,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _embeddingService = embeddingService;
        _timeProvider = timeProvider;
    }

    public async Task<Guid> IngestKnowledgeAsync(
        Guid tenantId,
        AiKnowledgeType knowledgeType,
        Guid? carrierId,
        string title,
        string contentChunk,
        string metadataJson,
        CancellationToken cancellationToken = default)
    {
        var vector = await _embeddingService.GenerateEmbeddingAsync(contentChunk, cancellationToken);
        var vectorJson = JsonSerializer.Serialize(vector);

        var entity = AiKnowledgeEmbedding.Create(
            tenantId: tenantId,
            knowledgeType: knowledgeType,
            carrierId: carrierId,
            title: title,
            contentChunk: contentChunk,
            embeddingVectorJson: vectorJson,
            metadataJson: metadataJson,
            now: _timeProvider.GetUtcNow());

        _dbContext.AiKnowledgeEmbeddings.Add(entity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return entity.Id;
    }

    public async Task<IReadOnlyList<KnowledgeSearchResult>> SearchSimilarAsync(
        Guid tenantId,
        string query,
        int limit = 5,
        Guid? carrierId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<KnowledgeSearchResult>();
        }

        var queryVector = await _embeddingService.GenerateEmbeddingAsync(query, cancellationToken);

        // Fetch candidates strictly bounded by tenant_id (Spec §19.8)
        var queryable = _dbContext.AiKnowledgeEmbeddings
            .AsNoTracking()
            .Where(k => k.TenantId == tenantId);

        if (carrierId.HasValue)
        {
            queryable = queryable.Where(k => k.CarrierId == null || k.CarrierId == carrierId.Value);
        }

        var candidates = await queryable
            .Select(k => new
            {
                k.Id,
                k.TenantId,
                k.KnowledgeType,
                k.CarrierId,
                k.Title,
                k.ContentChunk,
                k.MetadataJson,
                k.EmbeddingVectorJson
            })
            .ToListAsync(cancellationToken);

        var results = new List<KnowledgeSearchResult>();

        foreach (var c in candidates)
        {
            float[]? candidateVector = null;
            try
            {
                candidateVector = JsonSerializer.Deserialize<float[]>(c.EmbeddingVectorJson);
            }
            catch
            {
                // Ignore malformed candidate vector
            }

            if (candidateVector == null || candidateVector.Length != queryVector.Length)
            {
                continue;
            }

            // Dot product equals cosine similarity since vectors are L2-normalized
            var similarity = 0.0f;
            for (var i = 0; i < queryVector.Length; i++)
            {
                similarity += queryVector[i] * candidateVector[i];
            }

            results.Add(new KnowledgeSearchResult(
                c.Id,
                c.TenantId,
                c.KnowledgeType,
                c.CarrierId,
                c.Title,
                c.ContentChunk,
                c.MetadataJson,
                similarity));
        }

        return results
            .OrderByDescending(r => r.SimilarityScore)
            .Take(limit)
            .ToList();
    }
}
