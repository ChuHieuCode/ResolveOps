using ResolveOps.Domain.Ai;

namespace ResolveOps.Modules.Ai.Services.Governance;

/// <summary>
/// Enforces confidence thresholds and safeguards according to Master Spec §0 (Rule 4) & §21.5.
/// </summary>
public static class ConfidencePolicyEngine
{
    public const decimal HighConfidenceThreshold = 0.90m;
    public const decimal MediumConfidenceThreshold = 0.70m;

    public static AiReviewStatus EvaluateReviewStatus(
        AiTaskType taskType,
        decimal confidence,
        bool isFinancialOrLegal)
    {
        // Spec Rule 4 & §21.5: Any financial or legal decision requires mandatory human review regardless of confidence
        if (isFinancialOrLegal)
        {
            return AiReviewStatus.PendingReview;
        }

        if (confidence >= HighConfidenceThreshold)
        {
            return AiReviewStatus.AutoApplied;
        }

        return AiReviewStatus.PendingReview;
    }
}
