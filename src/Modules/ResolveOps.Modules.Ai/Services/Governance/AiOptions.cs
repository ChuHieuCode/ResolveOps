namespace ResolveOps.Modules.Ai.Services.Governance;

/// <summary>
/// Configuration options for the AI & RAG subsystem, including kill switch and model settings.
/// Master Spec §24 Phase 18.
/// </summary>
public sealed class AiOptions
{
    public const string SectionName = "Ai";

    public bool IsEnabled { get; set; } = true;
    public string DefaultModelProvider { get; set; } = "DeterministicMock";
    public string DefaultModelName { get; set; } = "resolveops-eval-v1";
    public string PromptVersion { get; set; } = "2026-09-v1";
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxTokens { get; set; } = 2048;
    public Dictionary<string, bool> TenantOverrides { get; set; } = new();
}
