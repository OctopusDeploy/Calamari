using Calamari.Integration.Processes;
using Calamari.Shared.Commands;

namespace Calamari.Deployment.Conventions
{
    public class LogVariablesConvention : IInstallConvention, Calamari.Shared.Commands.IConvention
    {
        public void Install(RunningDeployment deployment)
        {
            deployment.Variables.LogVariables();
        }

        public void Run(IExecutionContext context)
        {
            context.Variables.LogVariables();
        }
    }
}