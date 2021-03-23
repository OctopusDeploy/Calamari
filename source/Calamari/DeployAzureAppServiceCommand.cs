using System;
using System.Collections.Generic;
using System.Text;
using Calamari.AzureAppService.Behaviors;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Pipeline;

namespace Calamari.AzureAppService
{
    [Command("deploy-azure-app-service", Description = "Extracts and installs a deployment package to an Azure Web Application as a zip file")]
    public class DeployAzureAppServiceCommand : PipelineCommand
    {
        protected override IEnumerable<IDeployBehaviour> Deploy(DeployResolver resolver)
        {
            yield return resolver.Create<AppDeployBehavior>();
            yield return resolver.Create<AzureAppServiceSettingsBehaviour>();
            yield return resolver.Create<RestartAzureWebAppBehaviour>();
        }
    }
}
