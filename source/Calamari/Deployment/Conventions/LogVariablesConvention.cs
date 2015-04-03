using Calamari.Integration.Processes;

namespace Calamari.Deployment.Conventions
{
    public class LogVariablesConvention : IInstallConvention
    {
        public void Install(RunningDeployment deployment)
        {
            deployment.Variables.LogVariables();
        }
    }
}