using System;
using Octopus.CoreUtilities;
using Octopus.Server.Extensibility.HostServices.Diagnostics;
using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.ActionHandlers;
using Sashimi.Server.Contracts.Calamari;
using Sashimi.Server.Contracts.CommandBuilders;
using Sashimi.Server.Contracts.Variables;

namespace Sashimi.Tests.Shared.Server
{
    public class TestActionHandlerContext<TCalamariProgram> : IActionHandlerContext
    {
        internal TestActionHandlerContext(ITaskLog taskLog)
        {
            TaskLog = taskLog;
        }

        public ITaskLog TaskLog { get; }
        public Maybe<DeploymentTargetType> DeploymentTargetType { get; set; } = Maybe<DeploymentTargetType>.None;
        public Maybe<string> DeploymentTargetName { get; set; } = Maybe<string>.None;
        IActionAndTargetScopedVariables IActionHandlerContext.Variables => Variables;
        public TestVariableDictionary Variables { get; } = new();
        public string EnvironmentId { get; set; } = null!;
        public Maybe<string> TenantId { get; set; } = Maybe<string>.None;

        public IRawShellCommandBuilder RawShellCommand()
        {
            throw new NotImplementedException();
        }

        public ICalamariCommandBuilder CalamariCommand(CalamariFlavour tool, string toolCommand)
        {
            var builder = new TestCalamariCommandBuilder<TCalamariProgram>(tool, toolCommand);

            builder.SetVariables(Variables);

            return builder;
        }

        public IScriptCommandBuilder ScriptCommand()
        {
            throw new NotImplementedException();
        }
    }
}