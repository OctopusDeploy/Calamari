using System.Collections.Specialized;
using System.IO;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;

namespace Calamari.Integration.Scripting.FSharp
{
    public class FSharpEngine : IScriptEngine
    {
        public ScriptType[] GetSupportedTypes()
        {
            return new[] {ScriptType.FSharp};
        }

        public CommandResult Execute(
            Script script, 
            CalamariVariableDictionary variables, 
            ICommandLineRunner commandLineRunner,
            StringDictionary environmentVars = null)
        {
            var workingDirectory = Path.GetDirectoryName(script.File);

            var executable = FSharpBootstrapper.FindExecutable();
            var configurationFile = FSharpBootstrapper.PrepareConfigurationFile(workingDirectory, variables);
            var boostrapFile = FSharpBootstrapper.PrepareBootstrapFile(script.File, configurationFile, workingDirectory);
            var arguments = FSharpBootstrapper.FormatCommandArguments(boostrapFile, script.Parameters);

            using (new TemporaryFile(configurationFile))
            using (new TemporaryFile(boostrapFile))
            {
                return commandLineRunner.Execute(new CommandLineInvocation(executable, arguments, workingDirectory, environmentVars));
            }
        }
    }
}