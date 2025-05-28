using System;
using System.Collections.Generic;
using Calamari.Azure.ServiceFabric.Behaviours;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Pipeline;

namespace Calamari.Azure.ServiceFabric
{
    [Command("health-check", Description = "Run a health check on a DeploymentTargetType")]
    public class HealthCheckCommand : PipelineCommand
    {
        protected override IEnumerable<IBeforePackageExtractionBehaviour> BeforePackageExtraction(BeforePackageExtractionResolver resolver)
        {
            yield return resolver.Create<CheckRunningOnWindowsBehaviour>();
            yield return resolver.Create<CheckSdkInstalledBehaviour>();
        }

        protected override IEnumerable<IDeployBehaviour> Deploy(DeployResolver resolver)
        {
            yield return resolver.Create<HealthCheckBehaviour>();
        }
    }
}