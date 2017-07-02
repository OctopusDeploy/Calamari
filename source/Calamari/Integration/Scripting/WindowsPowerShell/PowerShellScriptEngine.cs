using System.IO;
using System.Linq;
using System.Security;
using Calamari.Deployment;
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
            /*
                The script hash passed to PowerShellBootstrapper.FormatCommandArguments() to
                catch race conditions where the script is overwritten.
            */
            string hash;
            var bootstrapFile = PowerShellBootstrapper.PrepareBootstrapFile(script, variables, out hash);
            var debuggingBootstrapFile = PowerShellBootstrapper.PrepareDebuggingBootstrapFile(script);
            var arguments = PowerShellBootstrapper.FormatCommandArguments(bootstrapFile, debuggingBootstrapFile, variables, hash);

            var userName = variables.Get(SpecialVariables.Action.PowerShell.UserName);
            var password = ToSecureString(variables.Get(SpecialVariables.Action.PowerShell.Password));

            using (new TemporaryFile(bootstrapFile))
            {
                using (new TemporaryFile(debuggingBootstrapFile))
                {
                    var invocation = new CommandLineInvocation(executable, arguments, workingDirectory, userName, password);
                    return commandLineRunner.Execute(invocation);
                }
            }
        }

        static SecureString ToSecureString(string unsecureString)
        {
            if (string.IsNullOrEmpty(unsecureString))
                return null;

            return unsecureString.Aggregate(new SecureString(), (s, c) => {
                s.AppendChar(c);
                return s;
            });
        }
    }
}