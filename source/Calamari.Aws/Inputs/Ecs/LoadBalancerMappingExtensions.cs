using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Octopus.Calamari.Contracts.Aws.Ecs;
using Cfn = Calamari.Aws.Integration.Ecs.Deploy.Cfn;

namespace Calamari.Aws.Inputs.Ecs;

public static class LoadBalancerMappingExtensions
{
    public static Cfn.LoadBalancer[] ToLoadBalancerProperties(this IEnumerable<LoadBalancerMapping> loadBalancerMappings)
    {
        var mappings = loadBalancerMappings.Select(lbm => new Cfn.LoadBalancer
        {
            ContainerName  = lbm.ContainerName,
            ContainerPort  = lbm.ContainerPort.ConvertedOrDefault<double?>(s => double.Parse(s, CultureInfo.InvariantCulture)),
            TargetGroupArn = lbm.TargetGroupArn
        }).ToArray();

        return mappings.Length == 0 ? null : mappings;
    }
}
