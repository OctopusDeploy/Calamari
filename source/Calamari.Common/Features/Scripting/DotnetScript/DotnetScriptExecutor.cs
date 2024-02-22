using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting.DotnetScript;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Features.Scripting.DotnetScript
{
    public class DotnetScriptExecutor : ScriptExecutor
    {
        readonly ICommandLineRunner commandLineRunner;
        public DotnetScriptExecutor(ICommandLineRunner commandLineRunner)
        {
            this.commandLineRunner = commandLineRunner;
        }
        protected override IEnumerable<ScriptExecution> PrepareExecution(Script script,
            IVariables variables,
            Dictionary<string, string>? environmentVars = null)
        {
            var workingDirectory = Path.GetDirectoryName(script.File);
    
            // Fetch environment variables from Calamari variables
            var environmentVariables = variables
                                       .Where(t => t.Key.StartsWith("env:"))
                                       .ToDictionary(x => (x.Key).Replace("env:", ""), x => (string)x.Value);

            var hasDotnetToolInstalled = DotnetScriptBootstrapper.IsDotnetScriptToolInstalled(commandLineRunner, environmentVariables);
            var localDotnetScriptPath = DotnetScriptBootstrapper.DotnetScriptPath(commandLineRunner, environmentVariables);
            var bundledExecutable = DotnetScriptBootstrapper.FindBundledExecutable();

            var executable = GetExecutable(hasDotnetToolInstalled, localDotnetScriptPath, bundledExecutable);

            LogExecutionInfo(hasDotnetToolInstalled, localDotnetScriptPath, executable);

            var configurationFile = DotnetScriptBootstrapper.PrepareConfigurationFile(workingDirectory, variables);
            var (bootstrapFile, otherTemporaryFiles) = DotnetScriptBootstrapper.PrepareBootstrapFile(script.File, configurationFile, workingDirectory, variables);
            var arguments = DotnetScriptBootstrapper.FormatCommandArguments(bootstrapFile, script.Parameters);

            var cli = CreateCommandLineInvocation(executable, arguments, workingDirectory, hasDotnetToolInstalled);
            cli.EnvironmentVars = environmentVariables;

            yield return new ScriptExecution(cli, otherTemporaryFiles.Concat(new[] { bootstrapFile, configurationFile }));
        }
        
        private string GetExecutable(bool hasDotnetToolInstalled, string localDotnetScriptPath, string bundledExecutable)
        {
            return hasDotnetToolInstalled
                ? "dotnet-script"
                : string.IsNullOrEmpty(localDotnetScriptPath)
                    ? bundledExecutable
                    : localDotnetScriptPath;
        }

        private void LogExecutionInfo(bool hasDotnetToolInstalled, string localDotnetScriptPath, string executable)
        {
            Log.Verbose(hasDotnetToolInstalled
                            ? "Found dotnet-script tool installed locally, executing dotnet-script directly."
                            : string.IsNullOrEmpty(localDotnetScriptPath)
                                ? "Executing bundled dotnet-script"
                                : $"Found dotnet-script executable at {localDotnetScriptPath}");
        }

        private CommandLineInvocation CreateCommandLineInvocation(string executable, string arguments, string workingDirectory, bool hasDotnetToolInstalled)
        {
            if (CalamariEnvironment.IsRunningOnWindows || hasDotnetToolInstalled)
            {
                return new CommandLineInvocation(executable, arguments);
            }

            return new CommandLineInvocation("dotnet", $"\"{executable}\"", arguments)
            {
                WorkingDirectory = workingDirectory
            };
        }
    }
}