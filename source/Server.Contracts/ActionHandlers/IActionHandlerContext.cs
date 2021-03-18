using Octopus.CoreUtilities;
using Sashimi.Server.Contracts.Calamari;
using Sashimi.Server.Contracts.CommandBuilders;
using Sashimi.Server.Contracts.Variables;

namespace Sashimi.Server.Contracts.ActionHandlers
{
    public interface IActionHandlerContext
    {
        Maybe<DeploymentTargetType> DeploymentTargetType { get; }
        Maybe<string> DeploymentTargetName { get; }
        IActionAndTargetScopedVariables Variables { get; }
        string EnvironmentId { get; }
        Maybe<string> TenantId { get; }
        IRawShellCommandBuilder RawShellCommand();
        ICalamariCommandBuilder CalamariCommand(CalamariFlavour tool, string toolCommand);
        IScriptCommandBuilder ScriptCommand();
    }
}