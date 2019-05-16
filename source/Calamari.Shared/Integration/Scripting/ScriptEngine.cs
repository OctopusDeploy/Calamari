using System.Collections.Generic;
using System.IO;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;

namespace Calamari.Integration.Scripting
{
    public abstract class ScriptEngine : IScriptEngine
    {
        public abstract ScriptSyntax[] GetSupportedTypes();

        public CommandResult Execute(Script script, CalamariVariableDictionary variables, ICommandLineRunner commandLineRunner,
            Dictionary<string, string> environmentVars = null)
        {
            var prepared = PrepareExecution(script, variables, environmentVars);

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