﻿using System;
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
            var arguments = DotnetScriptBootstrapper.FormatCommandArguments(bootstrapFile, script.Parameters);
            bool.TryParse(variables.Get("Octopus.Action.Script.CSharp.BypassIsolation", "false"), out var bypassDotnetScriptIsolation);

            var cli = CreateCommandLineInvocation(executable, arguments, !string.IsNullOrWhiteSpace(localDotnetScriptPath));
            cli.EnvironmentVars = environmentVars;
            cli.WorkingDirectory = workingDirectory;
            cli.Isolate = !bypassDotnetScriptIsolation;

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
            var extension = Path.GetExtension(executable);
            
            return (CalamariEnvironment.IsRunningOnWindows || (hasDotnetToolOnPath && extension != ".dll"))
                ? new CommandLineInvocation(executable, arguments)
                : new CommandLineInvocation("dotnet", $"\"{executable}\"", arguments);
        }
    }
}