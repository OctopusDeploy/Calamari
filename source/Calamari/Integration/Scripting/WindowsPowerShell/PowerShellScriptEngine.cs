using System.IO;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;

namespace Calamari.Integration.Scripting.WindowsPowerShell
{
    public class PowerShellScriptEngine : IScriptEngine
    {
        public ScriptType[] GetSupportedTypes()
        {
            return new[] {ScriptType.Powershell};
        }

        public CommandResult Execute(Script script, CalamariVariableDictionary variables, ICommandLineRunner commandLineRunner)
        {
            var workingDirectory = Path.GetDirectoryName(script.File);

            var executable = PowerShellBootstrapper.PathToPowerShellExecutable();
            var boostrapFile = PowerShellBootstrapper.PrepareBootstrapFile(script, variables);
            var arguments = PowerShellBootstrapper.FormatCommandArguments(boostrapFile, variables);

            using (new TemporaryFile(boostrapFile))
            {
                var invocation = new CommandLineInvocation(executable, arguments, workingDirectory);
                return commandLineRunner.Execute(invocation);
            }
        }
    }
}