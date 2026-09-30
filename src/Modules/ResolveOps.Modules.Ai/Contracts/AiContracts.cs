namespace ResolveOps.Modules.Ai.Contracts;

// ── 1. Email & Note Classification ──────────────────────────────────────────
public sealed record CandidateExceptionType(string Type, decimal Confidence);

public sealed record ClassificationEntities(
    string? TrackingNumber,
    int? DamagedPackageCount,
    bool? PodNoted
);

public sealed record EmailClassificationOutput(
    IReadOnlyList<CandidateExceptionType> CandidateExceptionTypes,
    string SeveritySuggestion,
    ClassificationEntities Entities,
    IReadOnlyList<string> RecommendedEvidenceTypes,
    bool RequiresHumanReview
);

// ── 2. Timeline Summarization with Source Citations ──────────────────────────
public sealed record SourceCitation(
    string CitationKey,
    string EntityType,
    string EntityId
);

public sealed record TimelineSummaryOutput(
    string Summary,
    IReadOnlyList<string> KeyFindings,
    IReadOnlyList<SourceCitation> SourceCitations
);

// ── 3. RAG-Driven Evidence Recommendation ────────────────────────────────────
public sealed record RecommendedEvidenceItem(
    string EvidenceType,
    string Reason,
    string? CarrierRuleReference,
    string Priority
);

public sealed record EvidenceRecommendationOutput(
    IReadOnlyList<RecommendedEvidenceItem> MissingEvidenceTypes,
    decimal CurrentEvidenceSufficiencyScore,
    string AdvisoryNotice
);

// ── 4. RAG-Driven Draft Communication ────────────────────────────────────────
public sealed record DraftCommunicationOutput(
    string Subject,
    string Body,
    string RecipientRole,
    IReadOnlyList<string> ReferencedClauses,
    bool RequiresHumanApproval = true
);
