using System;
using Octopus.CoreUtilities;
using Octopus.Diagnostics;
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
        public ILog Log { get; } = null!;
        public Maybe<DeploymentTargetType> DeploymentTargetType { get; } = null!;
        public Maybe<string> DeploymentTargetName { get; } = null!;
        public IActionAndTargetScopedVariables Variables { get; }
        public string EnvironmentId { get; } = null!;
        public Maybe<string> TenantId { get; } = null!;

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