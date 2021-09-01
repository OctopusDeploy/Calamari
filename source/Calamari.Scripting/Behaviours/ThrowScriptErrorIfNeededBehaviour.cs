using System;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Pipeline;
using Calamari.Scripting;

namespace Calamari.Commands
{
    class ThrowScriptErrorIfNeededBehaviour : IOnFinishBehaviour
    {
        public bool IsEnabled(RunningDeployment context)
        {
            return true;
        }

        public Task Execute(RunningDeployment context)
        {
            var exitCode = context.Variables.GetInt32(SpecialVariables.Action.Script.ExitCode);
            if (exitCode != 0)
                throw new CommandException($"Script returned non-zero exit code: {exitCode}.");
            return this.CompletedTask();
        }
    }
}