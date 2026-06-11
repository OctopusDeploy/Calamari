using Calamari.Aws.Kubernetes.Discovery;
using Calamari.Common.Plumbing.Logging;
using Newtonsoft.Json;
using Octopus.Calamari.Contracts.TargetDiscovery;

namespace Calamari.Aws.Discovery;

/// <summary>
/// Deserializes the Discovery Context into AWS Credentials
/// </summary>
public static class AwsTargetDiscoveryContextResolver
{
    public static bool TryResolve(string contextJson, ILog log, out TargetDiscoveryContext<IAwsAuthenticationDetails> context)
    {
        context = null;
        try
        {
            context = JsonConvert.DeserializeObject<TargetDiscoveryContext<IAwsAuthenticationDetails>>(contextJson);
        }
        catch (JsonException ex)
        {
            log.Warn($"AWS target discovery context is in the wrong format: {ex.Message}");
            return false;
        }

        if (context?.Authentication == null || context?.Scope == null)
        {
            log.Warn("AWS target discovery context is in the wrong format.");
            context = null;
            return false;
        }

        return true;
    }
}
