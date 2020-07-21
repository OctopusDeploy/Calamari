using Calamari.Common.Commands;
using Calamari.Common.Features.Behaviours;

namespace Calamari.Deployment.Conventions
{
    public class JsonConfigurationVariablesConvention : IInstallConvention
    {
        readonly JsonConfigurationVariablesBehaviour jsonConfigurationVariablesBehaviour;

        public JsonConfigurationVariablesConvention(JsonConfigurationVariablesBehaviour jsonConfigurationVariablesBehaviour)
        {
            this.jsonConfigurationVariablesBehaviour = jsonConfigurationVariablesBehaviour;
        }

        public void Install(RunningDeployment deployment)
        {
            if (jsonConfigurationVariablesBehaviour.IsEnabled(deployment))
            {
                jsonConfigurationVariablesBehaviour.Execute(deployment).Wait();
            }
        }
    }
}