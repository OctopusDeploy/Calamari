using Calamari.Integration.Processes;
using Calamari.Shared.Commands;

namespace Calamari.Deployment.Conventions
{
    public class ContributeEnvironmentVariablesConvention : IInstallConvention, Calamari.Shared.Commands.IConvention
    {
        public void Install(RunningDeployment deployment)
        {
            // Allow scripts and other conventions to access environment variables. 
            // For example, #{env:SystemRoot} is equivalent to %SystemRoot%
            // This technique is also used for Tentacle (the host process that invokes Calamari) to pass variables in

            deployment.Variables.EnrichWithEnvironmentVariables();
        }

        public void Run(IExecutionContext context)
        {
            context.Variables.EnrichWithEnvironmentVariables();
        }
    }
}