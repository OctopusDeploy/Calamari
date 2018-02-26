using System.Collections.Specialized;
using System.IO;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;

namespace Calamari.Integration.Scripting.ScriptCS
{
    public class ScriptCSScriptEngine : IScriptEngine
    {
        public ScriptType[] GetSupportedTypes()
        {
            return new[] {ScriptType.ScriptCS};
        }

        public CommandResult Execute(
            Script script, 
            CalamariVariableDictionary variables, 
            ICommandLineRunner commandLineRunner,
            StringDictionary environmentVars = null)
        {
            var workingDirectory = Path.GetDirectoryName(script.File);

            var executable = ScriptCSBootstrapper.FindExecutable();
            var configurationFile = ScriptCSBootstrapper.PrepareConfigurationFile(workingDirectory, variables);
            var boostrapFile = ScriptCSBootstrapper.PrepareBootstrapFile(script.File, configurationFile, workingDirectory);
            var arguments = ScriptCSBootstrapper.FormatCommandArguments(boostrapFile, script.Parameters);

            using (new TemporaryFile(configurationFile))
            using (new TemporaryFile(boostrapFile))
            {
                return commandLineRunner.Execute(new CommandLineInvocation(executable, arguments, workingDirectory, environmentVars));
            }
        }
    }
}