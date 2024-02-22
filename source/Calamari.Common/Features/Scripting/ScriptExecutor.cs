using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Processes.Semaphores;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Proxies;
using Calamari.Common.Plumbing.Variables;

namespace Calamari.Common.Features.Scripting
{
    public abstract class ScriptExecutor : IScriptExecutor
    {
        static readonly string CopyWorkingDirectoryVariable = "Octopus.Calamari.CopyWorkingDirectoryIncludingKeyTo";

        public CommandResult Execute(Script script,
            IVariables variables,
            ICommandLineRunner commandLineRunner,
            Dictionary<string, string>? environmentVars = null)
        {
            var environmentVariablesIncludingProxy = environmentVars ?? new Dictionary<string, string>();
            foreach (var proxyVariable in ProxyEnvironmentVariablesGenerator.GenerateProxyEnvironmentVariables())
                environmentVariablesIncludingProxy[proxyVariable.Key] = proxyVariable.Value;

            var prepared = PrepareExecution(script, variables, environmentVariablesIncludingProxy);

            CommandResult? result = null;
            foreach (var execution in prepared)
            {
                if (variables.IsSet(CopyWorkingDirectoryVariable))
                    CopyWorkingDirectory(variables,
                        execution.CommandLineInvocation.WorkingDirectory,
                        execution.CommandLineInvocation.Arguments);

                try
                {
                    if (execution.CommandLineInvocation.Isolate)
                        using (SemaphoreFactory.Get()
                            .Acquire("CalamariSynchronizeProcess",
                                "Waiting for other process to finish executing script"))
                        {
                            result = commandLineRunner.Execute(execution.CommandLineInvocation);
                        }
                    else
                        result = commandLineRunner.Execute(execution.CommandLineInvocation);

                    if (result.ExitCode != 0)
                        return result;
                }
                finally
                {
                    foreach (var temporaryFile in execution.TemporaryFiles)
                    {
                        var fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();
                        fileSystem.DeleteFile(temporaryFile, FailureOptions.IgnoreFailure);
                    }
                }
            }

            return result!;
        }

        protected abstract IEnumerable<ScriptExecution> PrepareExecution(Script script,
            IVariables variables,
            Dictionary<string, string>? environmentVars = null);

        static void CopyWorkingDirectory(IVariables variables, string workingDirectory, string arguments)
        {
            var fs = CalamariPhysicalFileSystem.GetPhysicalFileSystem();

            var copyToParent = Path.Combine(
                variables.Get(CopyWorkingDirectoryVariable),
                fs.RemoveInvalidFileNameChars(variables.Get(ProjectVariables.Name, "Non-Project")),
                variables.Get(DeploymentVariables.Id, "Non-Deployment"),
                fs.RemoveInvalidFileNameChars(variables.Get(ActionVariables.Name, "Non-Action"))
            );

            string copyTo;
            var n = 1;
            do
            {
                copyTo = Path.Combine(copyToParent, $"{n++}");
            } while (Directory.Exists(copyTo));

            Log.Verbose($"Copying working directory '{workingDirectory}' to '{copyTo}'");
            fs.CopyDirectory(workingDirectory, copyTo);
            File.WriteAllText(Path.Combine(copyTo, "Arguments.txt"), arguments);
            File.WriteAllText(Path.Combine(copyTo, "CopiedFromDirectory.txt"), workingDirectory);
        }

        protected class ScriptExecution
        {
            public ScriptExecution(CommandLineInvocation commandLineInvocation, IEnumerable<string> temporaryFiles)
            {
                CommandLineInvocation = commandLineInvocation;
                TemporaryFiles = temporaryFiles;
            }

            public CommandLineInvocation CommandLineInvocation { get; }
            public IEnumerable<string> TemporaryFiles { get; }
        }
    }
}