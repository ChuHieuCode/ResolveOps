using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResolveOps.Domain.Ai;

namespace ResolveOps.Modules.Ai.Infrastructure.Persistence;

public sealed class AiTaskConfiguration : IEntityTypeConfiguration<AiTask>
{
    public void Configure(EntityTypeBuilder<AiTask> builder)
    {
        builder.ToTable("ai_tasks");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.TenantId).IsRequired();

        builder.Property(t => t.TaskType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.SourceEntityType)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.SourceEntityId).IsRequired();

        builder.Property(t => t.Status)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(t => t.ModelProvider)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.ModelName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(t => t.PromptVersion)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(t => t.InputHash)
            .HasMaxLength(128)
            .IsRequired();

        builder.Property(t => t.OutputJson)
            .HasColumnType("nvarchar(max)");

        builder.Property(t => t.Confidence)
            .HasColumnType("decimal(5,4)");

        builder.Property(t => t.FailureCode)
            .HasMaxLength(100);

        builder.Property(t => t.ReviewStatus)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(t => t.ReviewedBy);
        builder.Property(t => t.ReviewedAtUtc);

        builder.Property(t => t.CreatedAtUtc).IsRequired();
        builder.Property(t => t.CompletedAtUtc);

        builder.Property(t => t.ConcurrencyStamp)
            .IsConcurrencyToken()
            .HasMaxLength(36)
            .IsRequired();

        // Indexes for performance and multi-tenant isolation
        builder.HasIndex(t => new { t.TenantId, t.TaskType, t.Status })
            .HasDatabaseName("ix_ai_tasks_tenant_task_status");

        builder.HasIndex(t => new { t.TenantId, t.SourceEntityType, t.SourceEntityId })
            .HasDatabaseName("ix_ai_tasks_tenant_source");

        builder.HasIndex(t => new { t.TenantId, t.ReviewStatus })
            .HasDatabaseName("ix_ai_tasks_tenant_review_status");
    }
}
