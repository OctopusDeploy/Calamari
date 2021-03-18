using System;
using Octopus.CoreUtilities;
using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.ActionHandlers;
using Sashimi.Server.Contracts.Calamari;
using Sashimi.Server.Contracts.CommandBuilders;
using Sashimi.Server.Contracts.Variables;

namespace Sashimi.Tests.Shared.Server
{
    class WrapperActionHandlerContext : IActionHandlerContext
    {
        public WrapperActionHandlerContext(IActionAndTargetScopedVariables variables)
        {
            Variables = variables;
        }

        public Maybe<DeploymentTargetType> DeploymentTargetType { get; } = Maybe<DeploymentTargetType>.None;
        public Maybe<string> DeploymentTargetName { get; } = Maybe<string>.None;
        public IActionAndTargetScopedVariables Variables { get; }
        public string EnvironmentId { get; } = null!;
        public Maybe<string> TenantId { get; } = Maybe<string>.None;

        public IRawShellCommandBuilder RawShellCommand()
        {
            throw new NotImplementedException();
        }

        public ICalamariCommandBuilder CalamariCommand(CalamariFlavour tool, string toolCommand)
        {
            throw new NotImplementedException();
        }

        public IScriptCommandBuilder ScriptCommand()
        {
            throw new NotImplementedException();
        }
    }
}