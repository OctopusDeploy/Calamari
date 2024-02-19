using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Features.Scripting.Python
{
    public class PythonScriptExecutor : ScriptExecutor
    {
        protected override IEnumerable<ScriptExecution> PrepareExecution(Script script,
            IVariables variables,
            ICommandLineRunner commandLineRunner,
            Dictionary<string, string>? environmentVars = null)
        {
            var executable = PythonBootstrapper.FindPythonExecutable();
            var workingDirectory = Path.GetDirectoryName(script.File);

            var dependencyInstallerFile = PythonBootstrapper.PrepareDependencyInstaller(workingDirectory);
            var dependencyInstallerArguments = PythonBootstrapper.FormatCommandArguments(dependencyInstallerFile, string.Empty);

            yield return new ScriptExecution(
                new CommandLineInvocation(executable, dependencyInstallerArguments)
                {
                    WorkingDirectory = workingDirectory,
                    EnvironmentVars = environmentVars,
                    Isolate = true
                },
                new[] { dependencyInstallerFile });

            var configurationFile = PythonBootstrapper.PrepareConfigurationFile(workingDirectory, variables);

            var (bootstrapFile, otherTemporaryFiles) = PythonBootstrapper.PrepareBootstrapFile(script, workingDirectory, configurationFile, variables);
            var arguments = PythonBootstrapper.FormatCommandArguments(bootstrapFile, script.Parameters);

            yield return new ScriptExecution(
                new CommandLineInvocation(executable, arguments)
                {
                    WorkingDirectory = workingDirectory,
                    EnvironmentVars = environmentVars
                },
                otherTemporaryFiles.Concat(new[] { bootstrapFile, configurationFile }));
        }
    }
}