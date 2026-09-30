namespace ResolveOps.Domain.Ai;

/// <summary>
/// Captures field-level human corrections and feedback on AI task outputs.
/// Master Spec §21.4 (ai_feedback).
/// </summary>
public sealed class AiFeedback
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid AiTaskId { get; private set; }
    public string? FieldPath { get; private set; }
    public string? OriginalValue { get; private set; }
    public string? CorrectedValue { get; private set; }
    public AiFeedbackType FeedbackType { get; private set; }
    public Guid ReviewerId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }

    private AiFeedback() { }

    public static AiFeedback Create(
        Guid tenantId,
        Guid aiTaskId,
        string? fieldPath,
        string? originalValue,
        string? correctedValue,
        AiFeedbackType feedbackType,
        Guid reviewerId,
        DateTimeOffset now)
    {
        return new AiFeedback
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            AiTaskId = aiTaskId,
            FieldPath = fieldPath,
            OriginalValue = originalValue,
            CorrectedValue = correctedValue,
            FeedbackType = feedbackType,
            ReviewerId = reviewerId,
            CreatedAtUtc = now
        };
    }
}
