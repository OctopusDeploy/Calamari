using Calamari.Common.Commands;
using Calamari.Common.Features.StructuredVariables;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Deployment.Conventions
{
    public class JsonConfigurationVariablesConvention : IInstallConvention
    {
        readonly IStructuredConfigVariablesService structuredConfigVariablesService;

        public JsonConfigurationVariablesConvention(IStructuredConfigVariablesService structuredConfigVariablesService)
        {
            this.structuredConfigVariablesService = structuredConfigVariablesService;
        }

        public void Install(RunningDeployment deployment)
        {
            if (deployment.Variables.GetFlag(PackageVariables.JsonConfigurationVariablesEnabled))
            {
                structuredConfigVariablesService.DoJsonVariableReplacement(deployment);
            }
            else if (deployment.Variables.GetFlag(ActionVariables.StructuredConfigurationVariablesEnabled))
            {
                structuredConfigVariablesService.DoStructuredVariableReplacement(deployment);
            }
        }
    }
}