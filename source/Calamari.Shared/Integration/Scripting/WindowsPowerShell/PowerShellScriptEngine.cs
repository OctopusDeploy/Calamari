using System.Collections.Specialized;
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
        public ScriptSyntax[] GetSupportedTypes()
        {
            return new[] {ScriptSyntax.Powershell};
        }

        public CommandResult Execute(
            Script script, 
            CalamariVariableDictionary variables, 
            ICommandLineRunner commandLineRunner,
            StringDictionary environmentVars = null)
        {
            var workingDirectory = Path.GetDirectoryName(script.File);

            var executable = PowerShellBootstrapper.PathToPowerShellExecutable();
            var bootstrapFile = PowerShellBootstrapper.PrepareBootstrapFile(script, variables);
            var debuggingBootstrapFile = PowerShellBootstrapper.PrepareDebuggingBootstrapFile(script);
            var arguments = PowerShellBootstrapper.FormatCommandArguments(bootstrapFile, debuggingBootstrapFile, variables);

            var userName = variables.Get(SpecialVariables.Action.PowerShell.UserName);
            var password = ToSecureString(variables.Get(SpecialVariables.Action.PowerShell.Password));

            using (new TemporaryFile(bootstrapFile))
            {
                using (new TemporaryFile(debuggingBootstrapFile))
                {
                    var invocation = new CommandLineInvocation(
                        executable, 
                        arguments, 
                        workingDirectory, 
                        environmentVars, 
                        userName, 
                        password);
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