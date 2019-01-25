using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using Calamari.Deployment;
using Calamari.Integration.Processes;

namespace Calamari.Integration.Scripting.WindowsPowerShell
{
    public class PowerShellScriptEngine : ScriptEngine
    {
        public override ScriptSyntax[] GetSupportedTypes()
        {
            return new[] {ScriptSyntax.PowerShell};
        }

        protected override ScriptExecution PrepareExecution(Script script, CalamariVariableDictionary variables,
            Dictionary<string, string> environmentVars = null)
        {
            var workingDirectory = Path.GetDirectoryName(script.File);

            var executable = PowerShellBootstrapper.PathToPowerShellExecutable();
            var bootstrapFile = PowerShellBootstrapper.PrepareBootstrapFile(script, variables);
            var debuggingBootstrapFile = PowerShellBootstrapper.PrepareDebuggingBootstrapFile(script);
            var arguments =
                PowerShellBootstrapper.FormatCommandArguments(bootstrapFile, debuggingBootstrapFile, variables);

            var userName = variables.Get(SpecialVariables.Action.PowerShell.UserName);
            var password = ToSecureString(variables.Get(SpecialVariables.Action.PowerShell.Password));

            return new ScriptExecution(
                new CommandLineInvocation(
                    executable,
                    arguments,
                    workingDirectory,
                    environmentVars,
                    userName,
                    password),
                new[] {bootstrapFile, debuggingBootstrapFile}
            );
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