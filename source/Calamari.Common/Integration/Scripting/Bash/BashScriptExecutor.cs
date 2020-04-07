using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Integration.Processes;

namespace Calamari.Integration.Scripting.Bash
{
    public class BashScriptExecutor : ScriptExecutor
    {
        protected override IEnumerable<ScriptExecution> PrepareExecution(Script script, IVariables variables,
            Dictionary<string, string> environmentVars = null)
        {
            var workingDirectory = Path.GetDirectoryName(script.File);
            var configurationFile = BashScriptBootstrapper.PrepareConfigurationFile(workingDirectory, variables);
            var (bootstrapFile, otherTemporaryFiles) = BashScriptBootstrapper.PrepareBootstrapFile(script, configurationFile, workingDirectory, variables);

            var invocation = new CommandLineInvocation(
                BashScriptBootstrapper.FindBashExecutable(),
                BashScriptBootstrapper.FormatCommandArguments(Path.GetFileName(bootstrapFile))
            )
            {
                WorkingDirectory = workingDirectory,
                EnvironmentVars = environmentVars
            };

            yield return new ScriptExecution(
                invocation,
                otherTemporaryFiles.Concat(new[] {bootstrapFile, configurationFile})
            );
        }
    }
}