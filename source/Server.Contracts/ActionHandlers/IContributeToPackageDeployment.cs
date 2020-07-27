using System;
using Sashimi.Server.Contracts.CommandBuilders;

namespace Sashimi.Server.Contracts.ActionHandlers
{
    public interface IContributeToPackageDeployment
    {
        bool CanContribute(DeploymentTargetType deploymentTargetType);
        bool IsValid(IActionHandlerContext context, out IActionHandlerResult result);
        ICalamariCommandBuilder Create(IActionHandlerContext context);
    }
}