using Calamari.Common.Commands;
using Calamari.Common.Features.Behaviours;

namespace Calamari.Deployment.Conventions
{
    public class StructuredConfigurationVariablesConvention : IInstallConvention
    {
        readonly StructuredConfigurationVariablesBehaviour structuredConfigurationVariablesBehaviour;

        public StructuredConfigurationVariablesConvention(StructuredConfigurationVariablesBehaviour structuredConfigurationVariablesBehaviour)
        {
            this.structuredConfigurationVariablesBehaviour = structuredConfigurationVariablesBehaviour;
        }

        public void Install(RunningDeployment deployment)
        {
            if (structuredConfigurationVariablesBehaviour.IsEnabled(deployment))
            {
                structuredConfigurationVariablesBehaviour.Execute(deployment).Wait();
            }
        }
    }
}