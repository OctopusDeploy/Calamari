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

            if (variables.IsSet(SpecialVariables.CopyWorkingDirectoryIncludingKeyTo))
            {
                CopyWorkingDirectory(variables, prepared.CommandLineInvocation.WorkingDirectory,
                    prepared.CommandLineInvocation.Arguments);
            }

            try
            {
                return commandLineRunner.Execute(prepared.CommandLineInvocation);
            }
            finally
            {
                foreach (var temporaryFile in prepared.TemporaryFiles)
                {
                    var fileSystem = CalamariPhysicalFileSystem.GetPhysicalFileSystem();
                    fileSystem.DeleteFile(temporaryFile, FailureOptions.IgnoreFailure);
                }
            }
        }

        protected abstract ScriptExecution PrepareExecution(Script script, CalamariVariableDictionary variables,
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