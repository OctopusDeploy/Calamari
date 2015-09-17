using System.IO;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;

namespace Calamari.Integration.Scripting.ScriptCS
{
    public class ScriptCSScriptEngine : IScriptEngine
    {
        public string[] GetSupportedExtensions()
        {
            return new[] {ScriptType.ScriptCS.FileExtension()};
        }

        public CommandResult Execute(string scriptFile, CalamariVariableDictionary variables, ICommandLineRunner commandLineRunner)
        {
            var workingDirectory = Path.GetDirectoryName(scriptFile);

            var executable = ScriptCSBootstrapper.FindScriptCSExecutable();
            var configurationFile = ScriptCSBootstrapper.PrepareConfigurationFile(workingDirectory, variables);
            var boostrapFile = ScriptCSBootstrapper.PrepareBootstrapFile(scriptFile, configurationFile, workingDirectory);
            var arguments = ScriptCSBootstrapper.FormatCommandArguments(boostrapFile);

            using (new TemporaryFile(configurationFile))
            using (new TemporaryFile(boostrapFile))
            {
                return commandLineRunner.Execute(new CommandLineInvocation(executable, arguments, workingDirectory));
            }
        }
    }
}