using System.Collections.Generic;
using Calamari.Aws.Behaviours;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Pipeline;

namespace Calamari.Aws.Commands;

[Command("aws-ecs-cluster-target-discovery", Description = "Discover AWS ECS Clusters")]
public class EcsClusterTargetDiscoveryCommand : PipelineCommand
{
    protected override IEnumerable<IDeployBehaviour> Deploy(DeployResolver resolver)
    {
        yield return resolver.Create<EcsClusterDiscoveryBehaviour>();
    }
}