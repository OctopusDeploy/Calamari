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
        public ScriptType[] GetSupportedTypes()
        {
            return new[] {ScriptType.Powershell};
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
                    var result = commandLineRunner.Execute(invocation);

                    if (variables.IsSet(SpecialVariables.CopyWorkingDirectoryIncludingKeyTo))
                        CopyWorkingDirectory(variables, workingDirectory, arguments);
                    
                    return result;
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
        
        
        static void CopyWorkingDirectory(CalamariVariableDictionary variables, string workingDirectory, string arguments)
        {
            var fs = CalamariPhysicalFileSystem.GetPhysicalFileSystem();

            var copyToParent = Path.Combine(
                variables.Get(SpecialVariables.CopyWorkingDirectoryIncludingKeyTo),
                fs.RemoveInvalidFileNameChars(variables.Get(SpecialVariables.Project.Name)),
                variables.Get(SpecialVariables.Deployment.Id),
                fs.RemoveInvalidFileNameChars(variables.Get(SpecialVariables.Action.Name))
            );

            string copyTo;
            var n = 1;
            do
            {
                copyTo = Path.Combine(copyToParent, $"{n++}");
            } while (Directory.Exists(copyTo));

            fs.CopyDirectory(workingDirectory, copyTo);
            File.WriteAllText(Path.Combine(copyTo, "PowershellArguments.txt"), arguments);
            File.WriteAllText(Path.Combine(copyTo, "CopiedFromDirectory.txt"), workingDirectory);
        }
    }
}