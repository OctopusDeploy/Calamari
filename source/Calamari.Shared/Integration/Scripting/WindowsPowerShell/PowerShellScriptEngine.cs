using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using Calamari.Commands.Support;
using Calamari.Deployment;
using Calamari.Integration.FileSystem;
using Calamari.Integration.Processes;

namespace Calamari.Integration.Scripting.WindowsPowerShell
{
    public class PowerShellScriptEngine : ScriptEngine
    {
        public override ScriptSyntax[] GetSupportedTypes()
        {
            return new[] {ScriptSyntax.PowerShell};
        }

        protected override IEnumerable<ScriptExecution> PrepareExecution(Script script,
            CalamariVariableDictionary variables,
            Dictionary<string, string> environmentVars = null)
        {
            var powerShellBootstrapper = GetPowerShellBootstrapper(variables);
            
            var workingDirectory = Path.GetDirectoryName(script.File);
            var executable = powerShellBootstrapper.PathToPowerShellExecutable(variables);
            var (bootstrapFile, otherTemporaryFiles) = powerShellBootstrapper.PrepareBootstrapFile(script, variables);
            var debuggingBootstrapFile = powerShellBootstrapper.PrepareDebuggingBootstrapFile(script);
            var arguments = powerShellBootstrapper.FormatCommandArguments(bootstrapFile, debuggingBootstrapFile, variables);

            var userName = powerShellBootstrapper.AllowImpersonation() ? variables.Get(SpecialVariables.Action.PowerShell.UserName) : null;
            var password = powerShellBootstrapper.AllowImpersonation() ? ToSecureString(variables.Get(SpecialVariables.Action.PowerShell.Password)) : null;

            yield return new ScriptExecution(
                new CommandLineInvocation(
                    executable,
                    arguments,
                    workingDirectory,
                    environmentVars,
                    userName,
                    password),
                otherTemporaryFiles.Concat(new[] {bootstrapFile, debuggingBootstrapFile})
            );
        }

        public PowerShellBootstrapper GetPowerShellBootstrapper(CalamariVariableDictionary variables)
        {
            if (CalamariEnvironment.IsRunningOnNix || CalamariEnvironment.IsRunningOnMac)
                return new UnixLikePowerShellCoreBootstrapper();
            
            var specifiedEdition = variables[SpecialVariables.Action.PowerShell.Edition];
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

            return unsecureString.Aggregate(new SecureString(), (s, c) => {
                s.AppendChar(c);
                return s;
            });
        }
        
        public class PowerShellEditionNotFoundException : CommandException
        {
            public PowerShellEditionNotFoundException(string specifiedEdition) 
                : base($"Attempted to use {specifiedEdition} edition of PowerShell, but this edition could not be found. Available editions: Core, Desktop")
            {
            }
        }
    }
}