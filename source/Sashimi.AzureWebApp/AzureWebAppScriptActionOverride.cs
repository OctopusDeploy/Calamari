using System;
using Sashimi.AzureScripting;
using Sashimi.AzureWebApp.Endpoints;
using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.ActionHandlers;

namespace Sashimi.AzureWebApp
{
    public class AzureWebAppScriptActionOverride : IScriptActionOverride
    {
        public ScriptActionOverrideResult ShouldOverride(DeploymentTargetType deploymentTargetType, IActionHandlerContext context)
        {
            return deploymentTargetType == AzureWebAppEndpoint.AzureWebAppDeploymentTargetType
                ? ScriptActionOverrideResult.RedirectToHandler<AzurePowerShellActionHandler>()
                :  ScriptActionOverrideResult.RunDefaultAction();
        }
    }
}