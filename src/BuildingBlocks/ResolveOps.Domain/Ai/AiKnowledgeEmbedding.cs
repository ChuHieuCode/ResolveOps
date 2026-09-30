namespace ResolveOps.Domain.Ai;

/// <summary>
/// Stores tenant-isolated knowledge chunks, vector representations, and metadata for RAG retrieval.
/// Master Spec §6.3, §19.8, §21.2.
/// </summary>
public sealed class AiKnowledgeEmbedding
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public AiKnowledgeType KnowledgeType { get; private set; }
    public Guid? CarrierId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string ContentChunk { get; private set; } = string.Empty;
    public string EmbeddingVectorJson { get; private set; } = "[]";
    public string MetadataJson { get; private set; } = "{}";
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private AiKnowledgeEmbedding() { }

    public static AiKnowledgeEmbedding Create(
        Guid tenantId,
        AiKnowledgeType knowledgeType,
        Guid? carrierId,
        string title,
        string contentChunk,
        string embeddingVectorJson,
        string metadataJson,
        DateTimeOffset now)
    {
        return new AiKnowledgeEmbedding
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            KnowledgeType = knowledgeType,
            CarrierId = carrierId,
            Title = title,
            ContentChunk = contentChunk,
            EmbeddingVectorJson = embeddingVectorJson,
            MetadataJson = metadataJson,
            CreatedAtUtc = now
        };
    }
}
