using System;
using System.Collections.Generic;
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
            yield return resolver.Create<AppDeployBehaviour>();
            yield return resolver.Create<AzureAppServiceSettingsBehaviourFactory>();
            yield return resolver.Create<RestartAzureWebAppBehaviourFactory>();
        }
    }

    [Command("deploy-azure-app-settings", Description = "Creates or updates existing app settings")]
    public class DeployAzureAppSettingsCommand : PipelineCommand
    {
        protected override IEnumerable<IDeployBehaviour> Deploy(DeployResolver resolver)
        {
            yield return resolver.Create<AzureAppServiceDeployBehaviourFactory>();
        }
    }
}
