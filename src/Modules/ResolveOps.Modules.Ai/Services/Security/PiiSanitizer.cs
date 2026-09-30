using System.Text.RegularExpressions;

namespace ResolveOps.Modules.Ai.Services.Security;

public sealed partial class PiiSanitizer : IPiiSanitizer
{
    // Regex for 13-19 digit credit cards
    [GeneratedRegex(@"\b(?:\d[ -]*?){13,19}\b", RegexOptions.Compiled)]
    private static partial Regex CreditCardRegex();

    // Regex for JWT Bearer tokens
    [GeneratedRegex(@"\beyJ[A-Za-z0-9-_]+\.[A-Za-z0-9-_]+\.[A-Za-z0-9-_]+\b", RegexOptions.Compiled)]
    private static partial Regex JwtRegex();

    // Regex for common API keys (e.g. sk-..., bearer tokens, long hex/base64 keys)
    [GeneratedRegex(@"\b(?:sk-[a-zA-Z0-9]{20,}|key-[a-zA-Z0-9]{20,})\b", RegexOptions.Compiled)]
    private static partial Regex ApiKeyRegex();

    // Regex for password/secret patterns (e.g. password: xyz, pwd=xyz)
    [GeneratedRegex(@"(?i)(password|secret|pwd)\s*[:=]\s*[^\s,;]+", RegexOptions.Compiled)]
    private static partial Regex PasswordRegex();

    public string Sanitize(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        var sanitized = input;

        sanitized = CreditCardRegex().Replace(sanitized, "[REDACTED_CREDIT_CARD]");
        sanitized = JwtRegex().Replace(sanitized, "[REDACTED_TOKEN]");
        sanitized = ApiKeyRegex().Replace(sanitized, "[REDACTED_API_KEY]");
        sanitized = PasswordRegex().Replace(sanitized, "$1: [REDACTED_CREDENTIAL]");

        return sanitized;
    }
}
