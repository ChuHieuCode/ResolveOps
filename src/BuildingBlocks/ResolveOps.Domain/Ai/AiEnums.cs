namespace ResolveOps.Domain.Ai;

public enum AiTaskType
{
    EmailClassification,
    NoteClassification,
    TimelineSummary,
    EvidenceRecommendation,
    DraftCommunication,
    SemanticSearch
}

public enum AiTaskStatus
{
    Pending,
    Processing,
    Completed,
    Failed,
    Rejected
}

public enum AiReviewStatus
{
    PendingReview,
    Approved,
    Corrected,
    Rejected,
    AutoApplied
}

public enum AiFeedbackType
{
    Correction,
    FalsePositive,
    Hallucination,
    Approved
}

public enum AiKnowledgeType
{
    CarrierPolicy,
    ClaimPrecedent,
    DisputeSOP
}
