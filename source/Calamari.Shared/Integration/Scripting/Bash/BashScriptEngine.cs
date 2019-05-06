using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Integration.Processes;

namespace Calamari.Integration.Scripting.Bash
{
    public class BashScriptEngine : ScriptEngine
    {
        public override ScriptSyntax[] GetSupportedTypes()
        {
            return new[] {ScriptSyntax.Bash};
        }

        protected override IEnumerable<ScriptExecution> PrepareExecution(Script script, CalamariVariableDictionary variables,
            Dictionary<string, string> environmentVars = null)
        {
            var workingDirectory = Path.GetDirectoryName(script.File);
            var configurationFile = BashScriptBootstrapper.PrepareConfigurationFile(workingDirectory, variables);
            var (bootstrapFile, otherTemporaryFiles) = BashScriptBootstrapper.PrepareBootstrapFile(script, configurationFile, workingDirectory, variables);

            yield return new ScriptExecution(
                new CommandLineInvocation(
                    BashScriptBootstrapper.FindBashExecutable(),
                    BashScriptBootstrapper.FormatCommandArguments(bootstrapFile), workingDirectory, environmentVars),
                    otherTemporaryFiles.Concat(new[] {bootstrapFile, configurationFile})
            );
        }
    }
}