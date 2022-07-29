using System.Collections.Generic;
using Calamari.AzureAppService.Behaviors;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Pipeline;

namespace Calamari.AzureAppService
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