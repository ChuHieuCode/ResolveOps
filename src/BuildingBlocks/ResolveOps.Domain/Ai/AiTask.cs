namespace ResolveOps.Domain.Ai;

/// <summary>
/// Represents an asynchronous AI processing task with provenance, model metadata, and review status.
/// Master Spec §21.4 (ai_tasks).
/// </summary>
public sealed class AiTask : IHasConcurrencyStamp
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public AiTaskType TaskType { get; private set; }
    public string SourceEntityType { get; private set; } = string.Empty;
    public Guid SourceEntityId { get; private set; }
    public AiTaskStatus Status { get; private set; }
    public string ModelProvider { get; private set; } = string.Empty;
    public string ModelName { get; private set; } = string.Empty;
    public string PromptVersion { get; private set; } = string.Empty;
    public string InputHash { get; private set; } = string.Empty;
    public string? OutputJson { get; private set; }
    public decimal? Confidence { get; private set; }
    public string? FailureCode { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? CompletedAtUtc { get; private set; }
    public AiReviewStatus? ReviewStatus { get; private set; }
    public Guid? ReviewedBy { get; private set; }
    public DateTimeOffset? ReviewedAtUtc { get; private set; }
    public string ConcurrencyStamp { get; set; } = Guid.NewGuid().ToString("N");

    private AiTask() { }

    public static AiTask Create(
        Guid tenantId,
        AiTaskType taskType,
        string sourceEntityType,
        Guid sourceEntityId,
        string inputHash,
        DateTimeOffset now)
    {
        return new AiTask
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            TaskType = taskType,
            SourceEntityType = sourceEntityType,
            SourceEntityId = sourceEntityId,
            Status = AiTaskStatus.Pending,
            InputHash = inputHash,
            CreatedAtUtc = now,
            ReviewStatus = AiReviewStatus.PendingReview
        };
    }

    public void MarkProcessing()
    {
        Status = AiTaskStatus.Processing;
    }

    public void Complete(
        string outputJson,
        decimal confidence,
        string modelProvider,
        string modelName,
        string promptVersion,
        AiReviewStatus reviewStatus,
        DateTimeOffset now)
    {
        OutputJson = outputJson;
        Confidence = confidence;
        ModelProvider = modelProvider;
        ModelName = modelName;
        PromptVersion = promptVersion;
        Status = AiTaskStatus.Completed;
        ReviewStatus = reviewStatus;
        CompletedAtUtc = now;
    }

    public void Fail(string failureCode, DateTimeOffset now)
    {
        Status = AiTaskStatus.Failed;
        FailureCode = failureCode;
        CompletedAtUtc = now;
    }

    public void Review(AiReviewStatus newStatus, Guid reviewerId, DateTimeOffset now)
    {
        ReviewStatus = newStatus;
        ReviewedBy = reviewerId;
        ReviewedAtUtc = now;
    }
}
