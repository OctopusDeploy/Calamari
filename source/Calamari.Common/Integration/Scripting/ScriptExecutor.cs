using System.Collections.Generic;
using System.IO;
using Calamari.Common.Variables;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Processes.Semaphores;
using Calamari.Integration.Proxies;

namespace Calamari.Common.Integration.Scripting
{
    public abstract class ScriptExecutor : IScriptExecutor
    {
        public CommandResult Execute(Script script, IVariables variables, ICommandLineRunner commandLineRunner,
            Dictionary<string, string> environmentVars = null)
        {
            var environmentVariablesIncludingProxy = environmentVars ?? new Dictionary<string, string>();
            foreach (var proxyVariable in ProxyEnvironmentVariablesGenerator.GenerateProxyEnvironmentVariables()) 
                environmentVariablesIncludingProxy[proxyVariable.Key] = proxyVariable.Value;

            var prepared = PrepareExecution(script, variables, environmentVariablesIncludingProxy);

            CommandResult result = null;
            foreach (var execution in prepared)
            {
                if (variables.IsSet(SpecialVariables.CopyWorkingDirectoryIncludingKeyTo))
                {
                    CopyWorkingDirectory(variables, execution.CommandLineInvocation.WorkingDirectory,
                        execution.CommandLineInvocation.Arguments);
                }

                try
                {
                    if (execution.CommandLineInvocation.Isolate)
                    {
                        using (SemaphoreFactory.Get().Acquire("CalamariSynchronizeProcess",
                            "Waiting for other process to finish executing script"))
                        { 
                            result = commandLineRunner.Execute(execution.CommandLineInvocation);
                        }
                    }
                    else
                    {
                        result = commandLineRunner.Execute(execution.CommandLineInvocation);
                    }

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

            return result;
        }

        protected abstract IEnumerable<ScriptExecution> PrepareExecution(Script script, IVariables variables,
            Dictionary<string, string> environmentVars = null);
        
        static void CopyWorkingDirectory(IVariables variables, string workingDirectory, string arguments)
        {
            var fs = CalamariPhysicalFileSystem.GetPhysicalFileSystem();

            var copyToParent = Path.Combine(
                variables.Get(SpecialVariables.CopyWorkingDirectoryIncludingKeyTo),
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