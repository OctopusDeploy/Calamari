using System.Collections.Specialized;
using System.IO;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;

namespace Calamari.Integration.Scripting.ScriptCS
{
    public class ScriptCSScriptEngine : ScriptEngine
    {
        public override ScriptSyntax[] GetSupportedTypes()
        {
            return new[] {ScriptSyntax.CSharp};
        }

        protected override ScriptExecution PrepareExecution(Script script, CalamariVariableDictionary variables,
            StringDictionary environmentVars = null)
        {
            var workingDirectory = Path.GetDirectoryName(script.File);
            var executable = ScriptCSBootstrapper.FindExecutable();
            var configurationFile = ScriptCSBootstrapper.PrepareConfigurationFile(workingDirectory, variables);
            var bootstrapFile = ScriptCSBootstrapper.PrepareBootstrapFile(script.File, configurationFile, workingDirectory);
            var arguments = ScriptCSBootstrapper.FormatCommandArguments(bootstrapFile, script.Parameters);

            return new ScriptExecution(
                new CommandLineInvocation(executable, arguments, workingDirectory, environmentVars),
                new[] {bootstrapFile, configurationFile}
            );
        }
    }
}