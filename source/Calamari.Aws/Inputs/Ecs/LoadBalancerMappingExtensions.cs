using System.Collections.Generic;
using System.Linq;
using Amazon.CDK.AWS.ECS;
using Octopus.Calamari.Contracts.Aws.Ecs;

namespace Calamari.Aws.Inputs.Ecs;

public static class LoadBalancerMappingExtensions
{
    public static CfnService.LoadBalancerProperty[] ToLoadBalancerProperties(this IEnumerable<LoadBalancerMapping> loadBalancerMappings)
    {
        return loadBalancerMappings.Select(lbm => new CfnService.LoadBalancerProperty
                                   {
                                        ContainerName =  lbm.ContainerName,
                                        ContainerPort = lbm.ContainerPort.ConvertedOrDefault<double?>(s => double.Parse(s)),
                                        TargetGroupArn =  lbm.TargetGroupArn,
                                   })
                                   .ToArray();
    }
}