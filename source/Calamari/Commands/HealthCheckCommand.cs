using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.HealthChecks;
using Calamari.Integration.Processes;

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
            var deploymentTargetTypeName = variables.Get(SpecialVariables.Machine.DeploymentTargetType);

            var checker = deploymentTargetTypeHealthCheckers.SingleOrDefault(x => x.HandlesDeploymentTargetTypeName(deploymentTargetTypeName));

            if (checker == null)
                throw new Exception($"No health checker could be found for deployment target type {deploymentTargetTypeName}");

            return checker.ExecuteHealthCheck(variables);
        }
    }
}