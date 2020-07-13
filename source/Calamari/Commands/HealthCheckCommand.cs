using System;
using System.Collections.Generic;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Commands.Support;
using Calamari.Common.Plumbing.Variables;
using Calamari.HealthChecks;

namespace Calamari.Commands
{
    [Command("health-check", Description = "Run a health check on a DeploymentTargetType")]
    public class HealthCheckCommand : Command
    {
        private readonly IEnumerable<IDoesDeploymentTargetTypeHealthChecks> deploymentTargetTypeHealthCheckers;
        readonly IVariables variables;

        public HealthCheckCommand(IEnumerable<IDoesDeploymentTargetTypeHealthChecks> deploymentTargetTypeHealthCheckers, IVariables variables)
        {
            this.deploymentTargetTypeHealthCheckers = deploymentTargetTypeHealthCheckers;
            this.variables = variables;
        }

        public override int Execute(string[] commandLineArguments)
        {
            var deploymentTargetTypeName = variables.Get(MachineVariables.DeploymentTargetType);

            var checker = deploymentTargetTypeHealthCheckers.SingleOrDefault(x => x.HandlesDeploymentTargetTypeName(deploymentTargetTypeName));

            if (checker == null)
                throw new Exception($"No health checker could be found for deployment target type {deploymentTargetTypeName}");

            return checker.ExecuteHealthCheck();
        }
    }
}