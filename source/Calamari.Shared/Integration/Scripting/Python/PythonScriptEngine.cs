using System.Collections.Specialized;
using System.IO;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Scripting.Bash;

namespace Calamari.Integration.Scripting.Python
{
    public class PythonScriptEngine : IScriptEngine
    {
        public ScriptSyntax[] GetSupportedTypes()
        {
            return new[] {ScriptSyntax.Python};
        }

        public CommandResult Execute(
            Script script,
            CalamariVariableDictionary variables,
            ICommandLineRunner commandLineRunner,
            StringDictionary environmentVars = null)
        {
            var executable = PythonBootstrapper.FindPythonExecutable();
            var workingDirectory = Path.GetDirectoryName(script.File);

            var configurationFile = PythonBootstrapper.PrepareConfigurationFile(workingDirectory, variables);
            var bootstrapFile = PythonBootstrapper.PrepareBootstrapFile(script, workingDirectory, configurationFile);
            var arguments = PythonBootstrapper.FormatCommandArguments(bootstrapFile);

            using (new TemporaryFile(configurationFile))
            using (new TemporaryFile(bootstrapFile))
            {
                return commandLineRunner.Execute(new CommandLineInvocation(executable, arguments, workingDirectory,
                    environmentVars));
            }
        }
    }
}