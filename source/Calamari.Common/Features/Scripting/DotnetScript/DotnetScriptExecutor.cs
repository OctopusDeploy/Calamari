using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Features.Scripting.DotnetScript
{
    public class DotnetScriptExecutor : ScriptExecutor
    {
        const string DotnetRollForwardVariableName = "DOTNET_ROLL_FORWARD";

        readonly ICommandLineRunner commandLineRunner;

        public DotnetScriptExecutor(ICommandLineRunner commandLineRunner, ILog log): base(log)
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
            var nugetSource = variables.Get("Octopus.Action.Script.CSharp.NuGetSource");
            var arguments = DotnetScriptBootstrapper.FormatCommandArguments(bootstrapFile, script.Parameters, nugetSource);
            bool.TryParse(variables.Get("Octopus.Action.Script.CSharp.BypassIsolation", "false"), out var bypassDotnetScriptIsolation);

            var cli = CreateCommandLineInvocation(executable, arguments, !string.IsNullOrWhiteSpace(localDotnetScriptPath));
            cli.EnvironmentVars = WithDotnetRollForward(environmentVars);
            cli.WorkingDirectory = workingDirectory;
            cli.Isolate = !bypassDotnetScriptIsolation;

            yield return new ScriptExecution(cli, otherTemporaryFiles.Concat(new[] { bootstrapFile, configurationFile }));
        }
        
        /// <summary>
        /// dotnet-script is a framework-dependent application - the bundled copy targets
        /// Microsoft.NETCore.App 8.0.0. By default a framework-dependent app will not roll forward
        /// across a major version, so on a machine that only has a newer runtime installed it fails
        /// to launch with "You must install or update .NET to run this application".
        ///
        /// Calamari itself is published self-contained and carries no such requirement; this affects
        /// only the separate dotnet-script process. Setting DOTNET_ROLL_FORWARD=Major lets it run on
        /// whatever newer runtime is present, so C# script steps don't additionally require the exact
        /// runtime dotnet-script was built against.
        /// </summary>
        static Dictionary<string, string> WithDotnetRollForward(Dictionary<string, string>? environmentVars)
        {
            var vars = environmentVars == null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string>(environmentVars);

            // Don't override an explicit value - the surrounding environment may have set one deliberately.
            if (!vars.ContainsKey(DotnetRollForwardVariableName))
                vars[DotnetRollForwardVariableName] = "Major";

            return vars;
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
            var extension = Path.GetExtension(executable);
            
            return (CalamariEnvironment.IsRunningOnWindows || (hasDotnetToolOnPath && extension != ".dll"))
                ? new CommandLineInvocation(executable, arguments)
                : new CommandLineInvocation("dotnet", $"\"{executable}\"", arguments);
        }
    }
}