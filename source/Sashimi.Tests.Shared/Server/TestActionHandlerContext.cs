using System;
using Calamari;
using Octopus.CoreUtilities;
using Sashimi.Server.Contracts;
using Sashimi.Server.Contracts.ActionHandlers;
using Sashimi.Server.Contracts.Calamari;
using Sashimi.Server.Contracts.CommandBuilders;
using Sashimi.Server.Contracts.Variables;
using ILog = Octopus.Diagnostics.ILog;

namespace Sashimi.Tests.Shared.Server
{
    public class TestActionHandlerContext<TCalamariProgram> : IActionHandlerContext where TCalamariProgram : CalamariFlavourProgram
    {
        readonly TestCalamariCommandBuilder<TCalamariProgram> calamariCommandBuilder;

        public TestActionHandlerContext(TestCalamariCommandBuilder<TCalamariProgram> calamariCommandBuilder)
        {
            this.calamariCommandBuilder = calamariCommandBuilder;
        }

        ILog IActionHandlerContext.Log { get; } = new ServerInMemoryLog();
        public Maybe<DeploymentTargetType> DeploymentTargetType { get; set; } = null!;
        public Maybe<string> DeploymentTargetName { get; set; } = null!;
        IActionAndTargetScopedVariables IActionHandlerContext.Variables => Variables;
        public TestVariableDictionary Variables { get; } = new TestVariableDictionary();
        public string EnvironmentId { get; set; } = null!;
        public Maybe<string> TenantId { get; set; } = null!;

        public IRawShellCommandBuilder RawShellCommand()
            => throw new NotImplementedException();

        public ICalamariCommandBuilder CalamariCommand(CalamariFlavour tool, string toolCommand)
        {
            calamariCommandBuilder.CalamariFlavour = tool;
            calamariCommandBuilder.CalamariCommand = toolCommand;
            return calamariCommandBuilder;
        }

        public IScriptCommandBuilder ScriptCommand()
            => throw new NotImplementedException();
    }
}