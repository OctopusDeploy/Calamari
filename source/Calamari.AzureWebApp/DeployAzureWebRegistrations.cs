using System.Collections.Generic;
using Calamari.Commands.Support;
using Calamari.CommonTemp;

namespace Calamari.AzureWebApp
{
    [Command("deploy-azure-web", Description = "Extracts and installs a deployment package to an Azure Web Application")]
    public class DeployAzureWebRegistrations : CommandPipelineRegistration
    {
        public override IEnumerable<IDeployBehaviour> Deploy(DeployResolver resolver)
        {
            yield return resolver.Create<AzureWebAppBehaviour>();
            yield return resolver.Create<LogAzureWebAppDetailsBehaviour>();
        }
    }
}