using System;
using Sashimi.AzureCloudService.Endpoints;
using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.ActionHandlers;

namespace Sashimi.AzureCloudService
{
    class AzureCloudServiceScriptActionOverride : IScriptActionOverride
    {
        public ScriptActionOverrideResult ShouldOverride(DeploymentTargetType deploymentTargetType, IActionHandlerContext context)
        {
            return deploymentTargetType == AzureCloudServiceEndpoint.AzureCloudServiceDeploymentTargetType
                ? ScriptActionOverrideResult.RedirectToHandler<AzureCloudServicePowerShellActionHandler>()
                :  ScriptActionOverrideResult.RunDefaultAction();
        }
    }
}