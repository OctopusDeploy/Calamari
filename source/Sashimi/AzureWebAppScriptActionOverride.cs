using System;
using Sashimi.AzureScripting;
using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.ActionHandlers;

namespace Sashimi.AzureWebApp
{
    class AzureWebAppScriptActionOverride : IScriptActionOverride
    {
        public ScriptActionOverrideResult ShouldOverride(DeploymentTargetType deploymentTargetType, IActionHandlerContext context)
        {
            return deploymentTargetType == AzureWebAppEndpoint.AzureWebAppDeploymentTargetType
                ? ScriptActionOverrideResult.RedirectToHandler<AzurePowerShellActionHandler>()
                :  ScriptActionOverrideResult.RunDefaultAction();
        }
    }
}