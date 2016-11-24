using System.IO;
using Calamari.Extensibility;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;

namespace Calamari.Integration.Scripting.Bash
{
    public class BashScriptEngine : IScriptEngine
    {
        public string[] GetSupportedExtensions()
        {
            return new[] {ScriptType.Bash.FileExtension()};
        }

        public CommandResult Execute(Script script, IVariableDictionary variables, ICommandLineRunner commandLineRunner)
        {
            var workingDirectory = Path.GetDirectoryName(script.File);
            var configurationFile = BashScriptBootstrapper.PrepareConfigurationFile(workingDirectory, variables);
            var boostrapFile = BashScriptBootstrapper.PrepareBootstrapFile(script, configurationFile, workingDirectory);

            using (new TemporaryFile(configurationFile))
            using (new TemporaryFile(boostrapFile))
            {
                return commandLineRunner.Execute(new CommandLineInvocation(
                    BashScriptBootstrapper.FindBashExecutable(),
                    BashScriptBootstrapper.FormatCommandArguments(boostrapFile), workingDirectory));
            }
        }
    }
}