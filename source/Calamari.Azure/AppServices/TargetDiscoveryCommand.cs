using System;
using System.Collections.Generic;
using Calamari.Azure.AppServices.Behaviors;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Pipeline;

namespace Calamari.Azure.AppServices
{
    [Command("target-discovery", Description = "Discover Azure web applications")]
    public class TargetDiscoveryCommand : PipelineCommand
    {
        protected override IEnumerable<IDeployBehaviour> Deploy(DeployResolver resolver)
        {
            yield return resolver.Create<TargetDiscoveryBehaviour>();
        }
    }
}