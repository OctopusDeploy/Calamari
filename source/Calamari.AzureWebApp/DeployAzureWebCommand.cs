using System.Collections.Generic;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Pipeline;

namespace Calamari.AzureWebApp
{
    [Command("deploy-azure-web", Description = "Extracts and installs a deployment package to an Azure Web Application")]
    public class DeployAzureWebCommand : PipelineCommand
    {
        protected override IEnumerable<IDeployBehaviour> Deploy(DeployResolver resolver)
        {
            yield return resolver.Create<AzureWebAppBehaviour>();
            yield return resolver.Create<LogAzureWebAppDetailsBehaviour>();
        }
    }
}