using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ResolveOps.Domain.Ai;

namespace ResolveOps.Modules.Ai.Infrastructure.Persistence;

public sealed class AiFeedbackConfiguration : IEntityTypeConfiguration<AiFeedback>
{
    public void Configure(EntityTypeBuilder<AiFeedback> builder)
    {
        builder.ToTable("ai_feedback");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.TenantId).IsRequired();

        builder.Property(f => f.AiTaskId).IsRequired();

        builder.Property(f => f.FieldPath)
            .HasMaxLength(250);

        builder.Property(f => f.OriginalValue)
            .HasColumnType("nvarchar(max)");

        builder.Property(f => f.CorrectedValue)
            .HasColumnType("nvarchar(max)");

        builder.Property(f => f.FeedbackType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(f => f.ReviewerId).IsRequired();

        builder.Property(f => f.CreatedAtUtc).IsRequired();

        builder.HasIndex(f => new { f.TenantId, f.AiTaskId })
            .HasDatabaseName("ix_ai_feedback_tenant_task");
    }
}
