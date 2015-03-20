using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using Octostache;

namespace Calamari.Integration.Scripting.WindowsPowerShell
{
    public class PowerShellScriptEngine : IScriptEngine
    {
        public CommandResult Execute(string scriptFile, VariableDictionary variables, ICommandLineRunner commandLineRunner)
        {
            var executable = PowerShellBootstrapper.PathToPowerShellExecutable();
            var boostrapFile = PowerShellBootstrapper.PrepareBootstrapFile(scriptFile, variables);
            var arguments = PowerShellBootstrapper.FormatCommandArguments(boostrapFile);

            using (new TemporaryFile(boostrapFile))
            {
                var invocation = new CommandLineInvocation(executable, arguments);
                return commandLineRunner.Execute(invocation);
            }
        }
    }
}