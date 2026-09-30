using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using ResolveOps.Domain.Ai;
using ResolveOps.Modules.Ai.Contracts;
using ResolveOps.Modules.Ai.Services.Security;

namespace ResolveOps.Modules.Ai.Services.Completion;

/// <summary>
/// Deterministic mock LLM completion service for offline evaluation, reproducible test execution, and CI benchmarks.
/// Produces valid structured JSON according to each use case schema and enforces prompt injection defense.
/// </summary>
public sealed class DeterministicMockAiCompletionService : IAiCompletionService
{
    private static readonly JsonSerializerOptions _indentedJsonOptions = new() { WriteIndented = true };
    private readonly IPiiSanitizer _piiSanitizer;

    public DeterministicMockAiCompletionService(IPiiSanitizer piiSanitizer)
    {
        _piiSanitizer = piiSanitizer;
    }

    public Task<AiCompletionResult<T>> CompleteStructuredAsync<T>(
        AiPromptRequest request,
        CancellationToken cancellationToken = default) where T : class
    {
        // 1. Sanitize PII
        var sanitizedInput = _piiSanitizer.Sanitize(request.UntrustedInput);

        // 2. Compute Input Hash (SHA-256 for provenance)
        var inputHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(sanitizedInput))).ToLowerInvariant();

        // 3. Prompt injection detection
        var hasPromptInjection = PromptInjectionShield.ContainsSuspiciousPatterns(sanitizedInput);

        var modelProvider = "DeterministicMock";
        var modelName = request.ModelName ?? "resolveops-eval-v1";
        var promptVersion = request.PromptVersion ?? "2026-09-v1";

        object resultObject;
        decimal confidence;

        if (hasPromptInjection)
        {
            // Adversarial prompt injection detected: safely shield and return safe defensive output
            (resultObject, confidence) = HandleAdversarialPayload<T>(sanitizedInput);
        }
        else if (typeof(T) == typeof(EmailClassificationOutput))
        {
            (resultObject, confidence) = GenerateClassification(sanitizedInput);
        }
        else if (typeof(T) == typeof(TimelineSummaryOutput))
        {
            (resultObject, confidence) = GenerateTimelineSummary(sanitizedInput);
        }
        else if (typeof(T) == typeof(EvidenceRecommendationOutput))
        {
            (resultObject, confidence) = GenerateEvidenceRecommendation(sanitizedInput, request.KnowledgeContext);
        }
        else if (typeof(T) == typeof(DraftCommunicationOutput))
        {
            (resultObject, confidence) = GenerateDraftCommunication(sanitizedInput, request.KnowledgeContext);
        }
        else
        {
            throw new NotSupportedException($"Target type {typeof(T).Name} is not supported by DeterministicMockAiCompletionService.");
        }

        var json = JsonSerializer.Serialize(resultObject, _indentedJsonOptions);
        var typedOutput = (T)resultObject;

        return Task.FromResult(new AiCompletionResult<T>(
            Output: typedOutput,
            RawJson: json,
            Confidence: confidence,
            ModelProvider: modelProvider,
            ModelName: modelName,
            PromptVersion: promptVersion,
            InputHash: inputHash,
            PromptInjectionDetected: hasPromptInjection));
    }

    private static (object Output, decimal Confidence) HandleAdversarialPayload<T>(string input)
    {
        if (typeof(T) == typeof(EmailClassificationOutput))
        {
            var output = new EmailClassificationOutput(
                CandidateExceptionTypes: [new CandidateExceptionType("SecurityIncident", 0.05m)],
                SeveritySuggestion: "Low",
                Entities: new ClassificationEntities(null, null, null),
                RecommendedEvidenceTypes: ["SecurityAuditLog"],
                RequiresHumanReview: true);
            return (output, 0.05m);
        }

        if (typeof(T) == typeof(TimelineSummaryOutput))
        {
            var output = new TimelineSummaryOutput(
                Summary: "Input contains untrusted instructions and was safely quarantined without execution.",
                KeyFindings: ["Adversarial prompt injection attempt detected and neutralized."],
                SourceCitations: [new SourceCitation("SYS_DEFENSE", "SecurityLog", Guid.Empty.ToString())]);
            return (output, 0.05m);
        }

        if (typeof(T) == typeof(EvidenceRecommendationOutput))
        {
            var output = new EvidenceRecommendationOutput(
                MissingEvidenceTypes: [],
                CurrentEvidenceSufficiencyScore: 0.0m,
                AdvisoryNotice: "Input rejected due to detected prompt injection patterns.");
            return (output, 0.05m);
        }

        if (typeof(T) == typeof(DraftCommunicationOutput))
        {
            var output = new DraftCommunicationOutput(
                Subject: "Security Notice: Suspicious Payload Quarantined",
                Body: "The system neutralized an adversarial prompt injection attempt in this document.",
                RecipientRole: "SecurityOfficer",
                ReferencedClauses: ["Spec §19.8 (Prompt Injection Defense)"],
                RequiresHumanApproval: true);
            return (output, 0.05m);
        }

        throw new NotSupportedException();
    }

    private static (EmailClassificationOutput Output, decimal Confidence) GenerateClassification(string text)
    {
        var lower = text.ToLowerInvariant();
        var types = new List<CandidateExceptionType>();
        var severity = "Medium";
        var recommended = new List<string>();

        if (lower.Contains("damage") || lower.Contains("broken") || lower.Contains("crushed") || lower.Contains("hỏng") || lower.Contains("vỡ"))
        {
            types.Add(new CandidateExceptionType("Damage", 0.94m));
            severity = "High";
            recommended.Add("DamagePhotos");
            recommended.Add("SignedPOD");
            recommended.Add("InspectionReport");
        }
        else if (lower.Contains("delay") || lower.Contains("late") || lower.Contains("chậm") || lower.Contains("trễ"))
        {
            types.Add(new CandidateExceptionType("Delay", 0.91m));
            severity = "Medium";
            recommended.Add("SignedPOD");
            recommended.Add("CarrierDeliveryReceipt");
        }
        else if (lower.Contains("loss") || lower.Contains("lost") || lower.Contains("missing") || lower.Contains("mất") || lower.Contains("thất lạc"))
        {
            types.Add(new CandidateExceptionType("Loss", 0.93m));
            severity = "High";
            recommended.Add("CommercialInvoice");
            recommended.Add("PackingList");
            recommended.Add("PoliceReport");
        }
        else if (lower.Contains("partial") || lower.Contains("thiếu") || lower.Contains("shortage"))
        {
            types.Add(new CandidateExceptionType("PartialDelivery", 0.88m));
            severity = "Medium";
            recommended.Add("SignedPOD");
            recommended.Add("PackingList");
        }
        else
        {
            types.Add(new CandidateExceptionType("GeneralException", 0.72m));
            severity = "Low";
            recommended.Add("SignedPOD");
        }

        // Tracking number regex
        var trackingMatch = Regex.Match(text, @"\b([A-Z0-9]{8,18})\b");
        var tracking = trackingMatch.Success ? trackingMatch.Value : "VN-TRK-987654";

        var entities = new ClassificationEntities(
            TrackingNumber: tracking,
            DamagedPackageCount: lower.Contains("package") || lower.Contains("kiện") ? 3 : 1,
            PodNoted: lower.Contains("pod") || lower.Contains("signed") || lower.Contains("ký"));

        var output = new EmailClassificationOutput(
            CandidateExceptionTypes: types,
            SeveritySuggestion: severity,
            Entities: entities,
            RecommendedEvidenceTypes: recommended,
            RequiresHumanReview: types[0].Confidence < 0.90m);

        return (output, types[0].Confidence);
    }

    private static (TimelineSummaryOutput Output, decimal Confidence) GenerateTimelineSummary(string text)
    {
        var summary = "Shipment experienced delay during transit leg [timeline_entry_leg1]. Carrier reported damaged packaging at destination hub [timeline_entry_hub2]. Consignee signed POD with exception note regarding outer carton puncture [document_pod_01].";

        var findings = new List<string>
        {
            "Transit delay flagged on day 3 [timeline_entry_leg1].",
            "Hub inspection noted box deformation [timeline_entry_hub2].",
            "Exception recorded on physical POD [document_pod_01]."
        };

        var citations = new List<SourceCitation>
        {
            new("timeline_entry_leg1", "TimelineEntry", Guid.NewGuid().ToString()),
            new("timeline_entry_hub2", "TimelineEntry", Guid.NewGuid().ToString()),
            new("document_pod_01", "EvidenceDocument", Guid.NewGuid().ToString())
        };

        var output = new TimelineSummaryOutput(summary, findings, citations);
        return (output, 0.92m);
    }

    private static (EvidenceRecommendationOutput Output, decimal Confidence) GenerateEvidenceRecommendation(string text, string? knowledgeContext)
    {
        var missing = new List<RecommendedEvidenceItem>
        {
            new("SignedPOD", "Carrier policy mandates physical or e-signature delivery receipt noting damage at delivery.", "Clause 4.1 (POD Exception Rule)", "High"),
            new("DamagePhotos", "Carrier requires high-resolution photos showing package barcode and damage location.", "Clause 5.3 (Photographic Evidence)", "High"),
            new("CommercialInvoice", "Required to substantiate replacement cost and claim amount.", "Clause 6.1 (Valuation Proof)", "Medium")
        };

        var notice = string.IsNullOrEmpty(knowledgeContext)
            ? "Recommendations derived from general logistics dispute policy."
            : "Recommendations enriched using RAG-retrieved carrier claim regulations.";

        var output = new EvidenceRecommendationOutput(
            MissingEvidenceTypes: missing,
            CurrentEvidenceSufficiencyScore: 0.65m,
            AdvisoryNotice: notice);

        return (output, 0.89m);
    }

    private static (DraftCommunicationOutput Output, decimal Confidence) GenerateDraftCommunication(string text, string? knowledgeContext)
    {
        var subject = "Formal Dispute Notice — Claim Rejection Appeal (Tracking VN-TRK-987654)";
        var body = """
                   Dear Carrier Claims Department,

                   We are formally appealing your initial decision regarding exception case VN-TRK-987654.
                   According to the Master Transport Agreement and Standard Operating Procedure:
                   1. The delivery receipt explicitly noted package distress at the time of transfer [document_pod_01].
                   2. Notice of intent to claim was provided within the mandatory 14-day SLA window.
                   3. Attached commercial invoices and inspection photographs substantiate the claimed value.

                   Please review the enclosed supplementary evidence and issue your adjusted settlement notice.

                   Sincerely,
                   Claims Specialist Operations
                   """;

        var clauses = new List<string>
        {
            "Carrier SLA Section 12 (Dispute Resolution)",
            "Carrier Policy Article 4 (Notice of Claim Deadlines)",
            "Master Logistics Agreement §7.2 (Liability Caps)"
        };

        var output = new DraftCommunicationOutput(
            Subject: subject,
            Body: body,
            RecipientRole: "CarrierExternal",
            ReferencedClauses: clauses,
            RequiresHumanApproval: true);

        return (output, 0.94m);
    }
}
