using System.IO;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Octostache;

namespace Calamari.Integration.Scripting.ScriptCS
{
    public class ScriptCSScriptEngine : IScriptEngine
    {
        public CommandResult Execute(string scriptFile, VariableDictionary variables, ICommandLineRunner commandLineRunner)
        {
            var workingDirectory = Path.GetDirectoryName(scriptFile);

            var configurationFile = ScriptCSBootstrapper.PrepareConfigurationFile(workingDirectory, variables);
            var boostrapFile = ScriptCSBootstrapper.PrepareBootstrapFile(scriptFile, configurationFile, workingDirectory);

            using (new TemporaryFile(configurationFile))
            using (new TemporaryFile(boostrapFile))
            {
                return commandLineRunner.Execute(new CommandLineInvocation(ScriptCSBootstrapper.FindScriptCSExecutable(), ScriptCSBootstrapper.FormatCommandArguments(boostrapFile)));
            }
        }
    }
}