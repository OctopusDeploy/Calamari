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
            var bypassIsolation = bool.TryParse(variables.Get("Octopus.Action.Script.CSharp.BypassIsolation", "false"), out var bypass) && bypass;

            var hasLegacyIsolatedLoadContextFlag = DotnetScriptBootstrapper.HasLegacyIsolatedLoadContextFlag(script.Parameters);

            // Before dotnet-script 2.0 these two settings were independent, so a step could bypass the script lock and still ask for
            // the isolated load context. BypassIsolation now turns both off, so warn rather than silently dropping the isolation request.
            if (hasLegacyIsolatedLoadContextFlag && bypassIsolation)
                Log.Warn($"Ignoring '{DotnetScriptBootstrapper.LegacyIsolatedLoadContextArgument}' in the script parameters: "
                         + "Octopus.Action.Script.CSharp.BypassIsolation is set to true, which from dotnet-script 2.0 on also turns off the "
                         + "isolated assembly load context. If this script needs isolation, remove Octopus.Action.Script.CSharp.BypassIsolation; "
                         + "isolation is the default without it.");
            else if (hasLegacyIsolatedLoadContextFlag)
                Log.Verbose($"Ignoring '{DotnetScriptBootstrapper.LegacyIsolatedLoadContextArgument}' in the script parameters: "
                            + "the isolated assembly load context is the default from dotnet-script 2.0 on, so the flag is "
                            + "redundant. Set Octopus.Action.Script.CSharp.BypassIsolation to true to turn isolation off instead.");
            
            var arguments = DotnetScriptBootstrapper.FormatCommandArguments(bootstrapFile, script.Parameters, nugetSource, bypassIsolation);
            
            var cli = CreateCommandLineInvocation(executable, arguments, !string.IsNullOrWhiteSpace(localDotnetScriptPath));
            cli.EnvironmentVars = environmentVars;
            cli.WorkingDirectory = workingDirectory;
            cli.Isolate = !bypassIsolation;

            yield return new ScriptExecution(cli, otherTemporaryFiles.Concat([bootstrapFile, configurationFile]));
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