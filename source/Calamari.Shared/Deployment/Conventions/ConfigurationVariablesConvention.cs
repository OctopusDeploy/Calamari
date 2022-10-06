using Calamari.Common.Commands;
using Calamari.Common.Features.Behaviours;

namespace Calamari.Deployment.Conventions
{
    public class ConfigurationVariablesConvention : IInstallConvention
    {
        readonly ConfigurationVariablesBehaviour configurationVariablesBehaviour;

        public ConfigurationVariablesConvention(ConfigurationVariablesBehaviour configurationVariablesBehaviour)
        {
            this.configurationVariablesBehaviour = configurationVariablesBehaviour;
        }

        public void Install(RunningDeployment deployment)
        {
            if (configurationVariablesBehaviour.IsEnabled(deployment))
            {
                configurationVariablesBehaviour.Execute(deployment).Wait();
            }
        }
    }
}
