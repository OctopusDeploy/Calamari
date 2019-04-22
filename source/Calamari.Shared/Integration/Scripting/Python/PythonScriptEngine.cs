using System.Collections.Generic;
using System.IO;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;

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
            Dictionary<string, string> environmentVars = null)
        {
            var executable = PythonBootstrapper.FindPythonExecutable();
            var workingDirectory = Path.GetDirectoryName(script.File);

            var dependencyInstallerFile = PythonBootstrapper.PrepareDependencyInstaller(workingDirectory);
            var dependencyInstallerArguments = PythonBootstrapper.FormatCommandArguments(dependencyInstallerFile, string.Empty);
            using (new TemporaryFile(dependencyInstallerFile))
            {
                var result = commandLineRunner.Execute(new CommandLineInvocation(executable, dependencyInstallerArguments,
                    workingDirectory,
                    environmentVars));

                if (result.ExitCode != 0)
                    return result;
            }

            var configurationFile = PythonBootstrapper.PrepareConfigurationFile(workingDirectory, variables);
            var bootstrapFile = PythonBootstrapper.PrepareBootstrapFile(script, workingDirectory, configurationFile, variables);
            var arguments = PythonBootstrapper.FormatCommandArguments(bootstrapFile, script.Parameters);

            using (new TemporaryFile(configurationFile))
            using (new TemporaryFile(bootstrapFile))
            {
                return commandLineRunner.Execute(new CommandLineInvocation(executable, arguments, workingDirectory,
                    environmentVars));
            }
        }
    }
}