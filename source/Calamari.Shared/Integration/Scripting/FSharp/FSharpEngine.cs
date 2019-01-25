using System.Collections.Generic;
using System.IO;
using Calamari.Integration.Processes;

namespace Calamari.Integration.Scripting.FSharp
{
    public class FSharpEngine : ScriptEngine
    {
        public override ScriptSyntax[] GetSupportedTypes()
        {
            return new[] {ScriptSyntax.FSharp};
        }

        protected override ScriptExecution PrepareExecution(Script script, CalamariVariableDictionary variables,
            Dictionary<string, string> environmentVars = null)
        {
            var workingDirectory = Path.GetDirectoryName(script.File);
            var executable = FSharpBootstrapper.FindExecutable();
            var configurationFile = FSharpBootstrapper.PrepareConfigurationFile(workingDirectory, variables);
            var bootstrapFile = FSharpBootstrapper.PrepareBootstrapFile(script.File, configurationFile, workingDirectory);
            var arguments = FSharpBootstrapper.FormatCommandArguments(bootstrapFile, script.Parameters);

            return new ScriptExecution(
                new CommandLineInvocation(executable, arguments, workingDirectory, environmentVars),
                new[] {bootstrapFile, configurationFile}
            );
        }
    }
}