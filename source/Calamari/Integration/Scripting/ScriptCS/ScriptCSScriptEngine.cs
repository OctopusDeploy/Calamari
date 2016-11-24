using System.IO;
using Calamari.Extensibility;
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

        public CommandResult Execute(Script script, IVariableDictionary variables, ICommandLineRunner commandLineRunner)
        {
            var workingDirectory = Path.GetDirectoryName(script.File);

            var executable = ScriptCSBootstrapper.FindExecutable();
            var configurationFile = ScriptCSBootstrapper.PrepareConfigurationFile(workingDirectory, variables);
            var boostrapFile = ScriptCSBootstrapper.PrepareBootstrapFile(script.File, configurationFile, workingDirectory);
            var arguments = ScriptCSBootstrapper.FormatCommandArguments(boostrapFile, script.Parameters);

            using (new TemporaryFile(configurationFile))
            using (new TemporaryFile(boostrapFile))
            {
                return commandLineRunner.Execute(new CommandLineInvocation(executable, arguments, workingDirectory));
            }
        }
    }
}