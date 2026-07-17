using Calamari.Common.Plumbing.Extensions;

namespace Calamari.Aws.Integration.Ecs;

public interface IEcsStackNameGenerator
{
    string Generate(string clusterName, string serviceName, string environmentId, string tenantId = "");
}

// Generates a deterministic CFN stack name when the user didn't supply a stack name.
// Mirrors SPF's getStackName
public class EcsStackNameGenerator : IEcsStackNameGenerator
{
    const int MaxLength = 128;

    public string Generate(string clusterName, string serviceName, string environmentId, string tenantId = "")
    {
#pragma warning disable CS0618 // SPF parity requires the lodash camelCase port; tracked for replacement.
        var stackName = $"cf-ecs-{clusterName.CamelCase()}" +
                        $"-{serviceName.CamelCase()}" +
                        $"-{environmentId.CamelCase()}" +
                        $"-{(string.IsNullOrEmpty(tenantId) ? "untenanted" : tenantId.CamelCase())}";
#pragma warning restore CS0618

        stackName = stackName.Trim();
        return stackName.Length <= MaxLength ? stackName : stackName[..MaxLength];
    }
}
