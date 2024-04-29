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
            
            var localDotnetScriptPath = DotnetScriptBootstrapper.DotnetScriptPath(commandLineRunner, environmentVars);
            var bundledExecutable = DotnetScriptBootstrapper.FindBundledExecutable();

            var executable = GetExecutable(localDotnetScriptPath, bundledExecutable);

            LogExecutionInfo(localDotnetScriptPath);

            var configurationFile = DotnetScriptBootstrapper.PrepareConfigurationFile(workingDirectory, variables);
            var (bootstrapFile, otherTemporaryFiles) = DotnetScriptBootstrapper.PrepareBootstrapFile(script.File, configurationFile, workingDirectory, variables);
            var arguments = DotnetScriptBootstrapper.FormatCommandArguments(bootstrapFile, script.Parameters);

            var cli = CreateCommandLineInvocation(executable, arguments, !string.IsNullOrWhiteSpace(localDotnetScriptPath));
            cli.EnvironmentVars = environmentVars;
            cli.WorkingDirectory = workingDirectory;

            yield return new ScriptExecution(cli, otherTemporaryFiles.Concat(new[] { bootstrapFile, configurationFile }));
        }
        
        private string GetExecutable(string? localDotnetScriptPath, string bundledExecutable)
        {
            return string.IsNullOrWhiteSpace(localDotnetScriptPath)
                    ? bundledExecutable
                    : localDotnetScriptPath;
        }

        void LogExecutionInfo(string? localDotnetScriptPath)
        {
            Log.Verbose(string.IsNullOrEmpty(localDotnetScriptPath)
                                ? "dotnet-script was not found, executing the bundled version"
                                : $"Found dotnet-script executable at {localDotnetScriptPath}");
        }

        CommandLineInvocation CreateCommandLineInvocation(string executable, string arguments, bool hasDotnetToolOnPath)
        {
            Log.Info($"##teamcity[message text=Testing logs]");
            Log.Info($"##teamcity[message text={CalamariEnvironment.IsRunningOnWindows.ToString()}]");
            Log.Info($"##teamcity[message text={hasDotnetToolOnPath.ToString()}]");
            Log.Info($"##teamcity[message text={executable}]");
            Log.Info($"##teamcity[message text={arguments}]");
            
            return (CalamariEnvironment.IsRunningOnWindows || hasDotnetToolOnPath)
                ? new CommandLineInvocation(executable, arguments)
                : new CommandLineInvocation("dotnet", $"\"{executable}\"", arguments);
        }
    }
}