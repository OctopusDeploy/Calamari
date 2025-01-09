using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.GoogleCloudAccounts;

namespace Calamari.GoogleCloudScripting
{
    public class GoogleCloudContextScriptWrapper : IScriptWrapper
    {
        private readonly ILog log;
        readonly IVariables variables;
        readonly ScriptSyntax[] supportedScriptSyntax = {ScriptSyntax.PowerShell, ScriptSyntax.Bash};

        public GoogleCloudContextScriptWrapper(ILog log, IVariables variables)
        {
            this.log = log;
            this.variables = variables;
        }

        public int Priority => ScriptWrapperPriorities.CloudAuthenticationPriority;

        public bool IsEnabled(ScriptSyntax syntax) => supportedScriptSyntax.Contains(syntax);

        public IScriptWrapper? NextWrapper { get; set; }

        public CommandResult ExecuteScript(Script script,
            ScriptSyntax scriptSyntax,
            ICommandLineRunner commandLineRunner,
            Dictionary<string, string>? environmentVars)
        {
            var workingDirectory = Path.GetDirectoryName(script.File)!;

            environmentVars ??= new Dictionary<string, string>();
            var oAuthConfiguration = new GcloudOAuthFileConfiguration(workingDirectory);
            var setupGCloudAuthentication = new SetupGCloudAuthentication(variables, log, commandLineRunner, environmentVars, oAuthConfiguration);

            var result = setupGCloudAuthentication.Execute();
            if (result.ExitCode != 0)
            {
                return result;
            }

            return NextWrapper!.ExecuteScript(script, scriptSyntax, commandLineRunner, environmentVars);
        }
    }
}