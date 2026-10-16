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
        const string RollForwardVariable = "Octopus.Action.Script.CSharp.RollForward";
        const string DisableIsolatedLoadContextVariable = "Octopus.Action.Script.CSharp.DisableIsolatedLoadContext";

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
            bool.TryParse(variables.Get(DisableIsolatedLoadContextVariable, "false"), out var disableIsolatedLoadContext);

            if (DotnetScriptBootstrapper.HasLegacyIsolatedLoadContextFlag(script.Parameters))
                Log.Verbose($"Ignoring '{DotnetScriptBootstrapper.LegacyIsolatedLoadContextArgument}' in the script parameters: "
                            + "the isolated assembly load context is the default from dotnet-script 2.0 on, so the flag is "
                            + $"redundant. Set {DisableIsolatedLoadContextVariable} to true to turn isolation off instead.");

            var arguments = DotnetScriptBootstrapper.FormatCommandArguments(bootstrapFile, script.Parameters, nugetSource, disableIsolatedLoadContext);
            bool.TryParse(variables.Get("Octopus.Action.Script.CSharp.BypassIsolation", "false"), out var bypassDotnetScriptIsolation);

            var cli = CreateCommandLineInvocation(executable, arguments, !string.IsNullOrWhiteSpace(localDotnetScriptPath));
            cli.EnvironmentVars = WithRollForwardOverride(environmentVars, variables.Get(RollForwardVariable));
            cli.WorkingDirectory = workingDirectory;
            cli.Isolate = !bypassDotnetScriptIsolation;

            yield return new ScriptExecution(cli, otherTemporaryFiles.Concat(new[] { bootstrapFile, configurationFile }));
        }

        /// <summary>
        /// The roll-forward default ships in the vendored dotnet-script.runtimeconfig.json
        /// (see source/IncludeDotNetScript.targets), which is process-scoped and covers both the
        /// Windows and Linux launch paths without Calamari having to do anything.
        ///
        /// This only handles the per-step override. DOTNET_ROLL_FORWARD sits above the
        /// runtimeconfig in the host's precedence order - measured on a Windows worker: a
        /// runtimeconfig asking for LatestMajor resolved to 8.0.27 rather than 10.0.8 purely
        /// because an inherited DOTNET_ROLL_FORWARD=Major outranked it. Unlike the runtimeconfig it
        /// also reaches a dotnet-script the customer installed themselves and put on the PATH,
        /// which is preferred over our bundled copy.
        ///
        /// Setting an environment variable leaks it into every process the customer's script goes
        /// on to start, so we only do it when a step has explicitly asked for a policy.
        /// </summary>
        static Dictionary<string, string>? WithRollForwardOverride(Dictionary<string, string>? environmentVars, string? rollForward)
        {
            if (string.IsNullOrWhiteSpace(rollForward))
                return environmentVars;

            var vars = environmentVars == null
                ? new Dictionary<string, string>()
                : new Dictionary<string, string>(environmentVars);

            vars[DotnetRollForwardVariableName] = rollForward;

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