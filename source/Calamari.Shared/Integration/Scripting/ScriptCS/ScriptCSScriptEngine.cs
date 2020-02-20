using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Integration.Processes;

namespace Calamari.Integration.Scripting.ScriptCS
{
    public class ScriptCSScriptEngine : ScriptEngine
    {
        public override ScriptSyntax[] GetSupportedTypes()
        {
            return new[] {ScriptSyntax.CSharp};
        }

        protected override IEnumerable<ScriptExecution> PrepareExecution(Script script, IVariables variables,
            Dictionary<string, string> environmentVars = null)
        {
            var workingDirectory = Path.GetDirectoryName(script.File);
            var executable = ScriptCSBootstrapper.FindExecutable();
            var configurationFile = ScriptCSBootstrapper.PrepareConfigurationFile(workingDirectory, variables);
            var (bootstrapFile, otherTemporaryFiles) = ScriptCSBootstrapper.PrepareBootstrapFile(script.File, configurationFile, workingDirectory, variables);
            var arguments = ScriptCSBootstrapper.FormatCommandArguments(bootstrapFile, script.Parameters);

            yield return new ScriptExecution(
                new CommandLineInvocation(executable, arguments, workingDirectory, environmentVars),
                    otherTemporaryFiles.Concat(new[] {bootstrapFile, configurationFile})
            );
        }
    }
}