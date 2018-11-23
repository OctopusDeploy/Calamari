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
        private string variablesFile;
        private string sensitiveVariablesFile;
        private string sensitiveVariablesPassword;

        public HealthCheckCommand(IEnumerable<IDoesDeploymentTargetTypeHealthChecks> deploymentTargetTypeHealthCheckers)
        {
            this.deploymentTargetTypeHealthCheckers = deploymentTargetTypeHealthCheckers;

            Options.Add("variables=", "Path to a JSON file containing variables.", v => variablesFile = Path.GetFullPath(v));
            Options.Add("sensitiveVariables=", "Password protected JSON file containing sensitive-variables.", v => sensitiveVariablesFile = v);
            Options.Add("sensitiveVariablesPassword=", "Password used to decrypt sensitive-variables.", v => sensitiveVariablesPassword = v);
        }

        public override int Execute(string[] commandLineArguments)
        {
            Options.Parse(commandLineArguments);
            var variables = new CalamariVariableDictionary(variablesFile, sensitiveVariablesFile, sensitiveVariablesPassword);

            var deploymentTargetTypeName = variables.Get(SpecialVariables.Machine.DeploymentTargetType);

            var checker = deploymentTargetTypeHealthCheckers.SingleOrDefault(x => x.HandlesDeploymentTargetTypeName(deploymentTargetTypeName));

            if (checker == null)
                throw new Exception($"No health checker could be found for deployment target type {deploymentTargetTypeName}");

            return checker.ExecuteHealthCheck(variables);
        }
    }
}