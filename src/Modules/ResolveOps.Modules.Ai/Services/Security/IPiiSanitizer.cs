namespace ResolveOps.Modules.Ai.Services.Security;

/// <summary>
/// Pre-filters and redacts PII, tokens, and credentials before external model calls.
/// Master Spec §0 (Rule 16) & §19.8.
/// </summary>
public interface IPiiSanitizer
{
    string Sanitize(string input);
}
