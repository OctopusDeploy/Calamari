using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using Calamari.Commands.Support;
using Calamari.Common.Variables;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;
using SpecialVariables = Calamari.Deployment.SpecialVariables;

namespace Calamari.Integration.Scripting.WindowsPowerShell
{
    public class PowerShellScriptExecutor : ScriptExecutor
    {
        protected override IEnumerable<ScriptExecution> PrepareExecution(Script script,
            IVariables variables,
            Dictionary<string, string> environmentVars = null)
        {
            var powerShellBootstrapper = GetPowerShellBootstrapper(variables);
            
            var (bootstrapFile, otherTemporaryFiles) = powerShellBootstrapper.PrepareBootstrapFile(script, variables);
            var debuggingBootstrapFile = powerShellBootstrapper.PrepareDebuggingBootstrapFile(script);
            
            var executable = powerShellBootstrapper.PathToPowerShellExecutable(variables);
            var arguments = powerShellBootstrapper.FormatCommandArguments(bootstrapFile, debuggingBootstrapFile, variables);

            var invocation = new CommandLineInvocation(executable, arguments)
            {
                EnvironmentVars = environmentVars, 
                WorkingDirectory = Path.GetDirectoryName(script.File), 
                UserName = powerShellBootstrapper.AllowImpersonation() ? variables.Get(PowershellVariables.Action.UserName) : null,
                Password = powerShellBootstrapper.AllowImpersonation() ? ToSecureString(variables.Get(PowershellVariables.Action.Password)) : null
            };

            yield return new ScriptExecution(
                invocation,
                otherTemporaryFiles.Concat(new[] {bootstrapFile, debuggingBootstrapFile})
            );
        }

        PowerShellBootstrapper GetPowerShellBootstrapper(IVariables variables)
        {
            if (CalamariEnvironment.IsRunningOnNix || CalamariEnvironment.IsRunningOnMac)
                return new UnixLikePowerShellCoreBootstrapper();
            
            var specifiedEdition = variables[PowershellVariables.Action.Edition];
            if (string.IsNullOrEmpty(specifiedEdition))
                return new WindowsPowerShellBootstrapper();
            
            if (specifiedEdition.Equals("Core", StringComparison.OrdinalIgnoreCase))
                return new WindowsPowerShellCoreBootstrapper(CalamariPhysicalFileSystem.GetPhysicalFileSystem());
            
            if (specifiedEdition.Equals("Desktop", StringComparison.OrdinalIgnoreCase))
                return new WindowsPowerShellBootstrapper();
            
            throw new PowerShellEditionNotFoundException(specifiedEdition);
        }

        static SecureString ToSecureString(string unsecureString)
        {
            if (string.IsNullOrEmpty(unsecureString))
                return null;

            return unsecureString.Aggregate(new SecureString(), (s, c) =>
            {
                s.AppendChar(c);
                return s;
            });
        }
    }
    
    public class PowerShellEditionNotFoundException : CommandException
    {
        public PowerShellEditionNotFoundException(string specifiedEdition) 
            : base($"Attempted to use '{specifiedEdition}' edition of PowerShell, but this edition could not be found. Possible editions: Core, Desktop")
        {
        }
    }
}