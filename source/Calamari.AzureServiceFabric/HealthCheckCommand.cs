using System;
using System.Collections.Generic;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Pipeline;

namespace Calamari.AzureServiceFabric
{
    [Command("health-check", Description = "Run a health check on a DeploymentTargetType")]
    public class HealthCheckCommand : PipelineCommand
    {
        protected override IEnumerable<IDeployBehaviour> Deploy(DeployResolver resolver)
        {
            yield return resolver.Create<HealthCheckBehaviour>();
        }
    }
}