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
            ICommandLineRunner commandLineRunner,
            Dictionary<string, string>? environmentVars = null)
        {
            var workingDirectory = Path.GetDirectoryName(script.File);
            
            var hasDotnetToolInstalled = DotnetScriptBootstrapper.IsDotnetScriptToolInstalled(commandLineRunner);
            var localDotnetScriptPath = DotnetScriptBootstrapper.DotnetScriptPath(commandLineRunner);
            var bundledExecutable = DotnetScriptBootstrapper.FindExecutable();
            
            var executable = hasDotnetToolInstalled ? "dotnet-script" : string.IsNullOrEmpty(localDotnetScriptPath) ? bundledExecutable : localDotnetScriptPath;
            
            Log.Verbose(hasDotnetToolInstalled ? "Found dotnet-script tool installed locally, executing dotnet-script directly." : string.IsNullOrEmpty(localDotnetScriptPath) ? "Executing bundled dotnet-script" : $"Found dotnet-script executable at {localDotnetScriptPath}");
            
            var configurationFile = DotnetScriptBootstrapper.PrepareConfigurationFile(workingDirectory, variables);
            var (bootstrapFile, otherTemporaryFiles) = DotnetScriptBootstrapper.PrepareBootstrapFile(script.File, configurationFile, workingDirectory, variables);
            var arguments = DotnetScriptBootstrapper.FormatCommandArguments(bootstrapFile, script.Parameters);
            var cli = new CommandLineInvocation(executable, arguments)
            {
                WorkingDirectory = workingDirectory,
                EnvironmentVars = environmentVars
            };

            yield return new ScriptExecution(cli, otherTemporaryFiles.Concat(new[] { bootstrapFile, configurationFile }));
        }
    }
}