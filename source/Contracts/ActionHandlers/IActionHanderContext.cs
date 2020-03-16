using System;
using Octopus.CoreUtilities;
using Octopus.Diagnostics;
using Octopus.Sashimi.Contracts.Calamari;
using Octopus.Sashimi.Contracts.CommandBuilders;
using Octopus.Sashimi.Contracts.Variables;

namespace Octopus.Sashimi.Contracts.ActionHandlers
{
    public interface IActionHanderContext
    {
        ILog Log { get; }
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