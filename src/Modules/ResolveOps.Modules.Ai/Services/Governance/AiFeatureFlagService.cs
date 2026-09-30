using Microsoft.Extensions.Options;

namespace ResolveOps.Modules.Ai.Services.Governance;

public interface IAiFeatureFlagService
{
    bool IsAiEnabled(Guid tenantId);
}

public sealed class AiFeatureFlagService : IAiFeatureFlagService
{
    private readonly IOptionsMonitor<AiOptions> _options;

    public AiFeatureFlagService(IOptionsMonitor<AiOptions> options)
    {
        _options = options;
    }

    public bool IsAiEnabled(Guid tenantId)
    {
        var current = _options.CurrentValue;
        if (!current.IsEnabled)
        {
            return false;
        }

        var key = tenantId.ToString("D");
        if (current.TenantOverrides.TryGetValue(key, out var enabled))
        {
            return enabled;
        }

        return true;
    }
}
