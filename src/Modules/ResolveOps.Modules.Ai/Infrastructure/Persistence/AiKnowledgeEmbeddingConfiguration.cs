using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResolveOps.Domain.Ai;

namespace ResolveOps.Modules.Ai.Infrastructure.Persistence;

public sealed class AiKnowledgeEmbeddingConfiguration : IEntityTypeConfiguration<AiKnowledgeEmbedding>
{
    public void Configure(EntityTypeBuilder<AiKnowledgeEmbedding> builder)
    {
        builder.ToTable("ai_knowledge_embeddings");

        builder.HasKey(k => k.Id);

        builder.Property(k => k.TenantId).IsRequired();

        builder.Property(k => k.KnowledgeType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(k => k.CarrierId);

        builder.Property(k => k.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(k => k.ContentChunk)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(k => k.EmbeddingVectorJson)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(k => k.MetadataJson)
            .HasColumnType("nvarchar(max)")
            .IsRequired();

        builder.Property(k => k.CreatedAtUtc).IsRequired();

        builder.HasIndex(k => new { k.TenantId, k.KnowledgeType, k.CarrierId })
            .HasDatabaseName("ix_ai_knowledge_tenant_type_carrier");

        builder.HasIndex(k => new { k.TenantId, k.CarrierId })
            .HasDatabaseName("ix_ai_knowledge_tenant_carrier");
    }
}
