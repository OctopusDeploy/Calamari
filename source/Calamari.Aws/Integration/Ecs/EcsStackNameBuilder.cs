#nullable enable
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Aws.Integration.Ecs;

// Generates a deterministic CFN stack name when the user didn't supply cfStackName.
// Mirrors SPF's getStackName
public static class EcsStackNameBuilder
{
    const int MaxLength = 128;

    public static string Build(IVariables variables, string clusterName, string serviceName)
    {
        var envId = variables.Get("Octopus.Environment.Id");
        var tenantId = variables.Get("Octopus.Deployment.Tenant.Id");

        var stackName = $"cf-ecs-{clusterName.CamelCase()}" +
                        $"-{serviceName.CamelCase()}" +
                        $"-{envId.CamelCase()}" +
                        $"-{(string.IsNullOrEmpty(tenantId) ? "untenanted" : tenantId.CamelCase())}";

        stackName = stackName.Trim();
        return stackName.Length <= MaxLength ? stackName : stackName[..MaxLength];
    }
}
