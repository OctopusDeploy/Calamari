using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Calamari.Integration.Processes.Semaphores;
using Calamari.Integration.Proxies;

namespace Calamari.Integration.Scripting
{
    public abstract class ScriptEngine : IScriptEngine
    {
        public abstract ScriptSyntax[] GetSupportedTypes();

        public CommandResult Execute(Script script, CalamariVariableDictionary variables, ICommandLineRunner commandLineRunner,
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

                if (variables.IsSet(SpecialVariables.Action.Script.Timeout))
                {
                    var timeout = variables.GetInt32(SpecialVariables.Action.Script.Timeout);
                    execution.CommandLineInvocation.Timeout = timeout.HasValue && timeout > 0 ? TimeSpan.FromMilliseconds(timeout.Value) : TimeSpan.MaxValue;

                    if (execution.CommandLineInvocation.Timeout == TimeSpan.Zero)
                    {
                        var link = "https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.process.waitforexit?view=netframework-4.8#System_Diagnostics_Process_WaitForExit_System_Int32_";
                        Log.Warn($"The timeout for this script was set to 0. Perhaps this was not intended. Setting the timeout to 0 will succeed only if the script exits immediately. See {link}. It will be ignored.");
                    }
                    else if (execution.CommandLineInvocation.Timeout > TimeSpan.Zero && execution.CommandLineInvocation.Timeout != TimeSpan.MaxValue)
                    {
                        Log.Verbose($"The script for this action will be executed with a timeout of {execution.CommandLineInvocation.Timeout} milliseconds. To remove this timeout, set the Action.Script.Timeout special variable to -1 or delete the variable.");
                    }
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

        protected abstract IEnumerable<ScriptExecution> PrepareExecution(Script script, CalamariVariableDictionary variables,
            Dictionary<string, string> environmentVars = null);
        
        static void CopyWorkingDirectory(CalamariVariableDictionary variables, string workingDirectory, string arguments)
        {
            var fs = CalamariPhysicalFileSystem.GetPhysicalFileSystem();

            var copyToParent = Path.Combine(
                variables.Get(SpecialVariables.CopyWorkingDirectoryIncludingKeyTo),
                fs.RemoveInvalidFileNameChars(variables.Get(SpecialVariables.Project.Name, "Non-Project")),
                variables.Get(SpecialVariables.Deployment.Id, "Non-Deployment"),
                fs.RemoveInvalidFileNameChars(variables.Get(SpecialVariables.Action.Name, "Non-Action"))
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