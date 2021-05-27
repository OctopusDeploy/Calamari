using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Proxies;
using Calamari.Common.Plumbing.Variables;

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
            var setupGCloudAuthentication = new SetupGCloudAuthentication(variables, log, commandLineRunner, environmentVars, workingDirectory);

            var result = setupGCloudAuthentication.Execute();
            if (result.ExitCode != 0)
            {
                return result;
            }

            return NextWrapper!.ExecuteScript(script, scriptSyntax, commandLineRunner, environmentVars);
        }

        class SetupGCloudAuthentication
        {
            readonly IVariables variables;
            readonly ILog log;
            readonly ICommandLineRunner commandLineRunner;
            readonly Dictionary<string, string> environmentVars;
            readonly string workingDirectory;
            private string gcloud = String.Empty;

            public SetupGCloudAuthentication(IVariables variables,
                ILog log,
                ICommandLineRunner commandLineRunner,
                Dictionary<string, string> environmentVars,
                string workingDirectory)
            {
                this.variables = variables;
                this.log = log;
                this.commandLineRunner = commandLineRunner;
                this.environmentVars = environmentVars;
                this.workingDirectory = workingDirectory;
            }

            public CommandResult Execute()
            {
                var errorResult = new CommandResult(string.Empty, 1);

                foreach (var proxyVariable in ProxyEnvironmentVariablesGenerator.GenerateProxyEnvironmentVariables())
                {
                    environmentVars[proxyVariable.Key] = proxyVariable.Value;
                }

                environmentVars.Add("CLOUDSDK_CORE_DISABLE_PROMPTS", "1");
                var gcloudConfigPath = Path.Combine(workingDirectory, "gcloud-cli");
                environmentVars.Add("CLOUDSDK_CONFIG", gcloudConfigPath);
                Directory.CreateDirectory(gcloudConfigPath);

                gcloud = variables.Get("Octopus.Action.GoogleCloud.CustomExecutable")!;
                if (!String.IsNullOrEmpty(gcloud))
                {
                    if (!File.Exists(gcloud))
                    {
                        log.Error($"The custom gcloud location of {gcloud} does not exist. Please make sure gcloud is installed in that location.");
                        return errorResult;
                    }
                }
                else
                {
                    gcloud = "gcloud";

                    if (ExecuteCommand("version").ExitCode != 0)
                    {
                        log.Error($"Could not find {gcloud}. Make sure {gcloud} is on the PATH.");
                        return errorResult;
                    }
                }

                log.Verbose("Authenticating to gcloud with key file");
                var jsonKey = variables.Get(SpecialVariables.Action.GoogleCloud.JsonKey, String.Empty);
                var bytes = Convert.FromBase64String(jsonKey);
                using (var keyFile = new TemporaryFile(Path.Combine(workingDirectory, Path.GetRandomFileName())))
                {
                    File.WriteAllBytes(keyFile.FilePath, bytes);
                    if (ExecuteCommand("auth", "activate-service-account", $"--key-file=\"{keyFile.FilePath}\"")
                        .ExitCode != 0)
                    {
                        log.Error("Failed to authenticate with gcloud.");
                        return errorResult;
                    }
                }
                log.Verbose("Successfully authenticated with gcloud");

                return new CommandResult(string.Empty, 0);
            }

            CommandResult ExecuteCommand(params string[] arguments)
            {
                var invocation = new CommandLineInvocation(gcloud, arguments)
                {
                    EnvironmentVars = environmentVars,
                    WorkingDirectory = workingDirectory,
                    OutputAsVerbose = true,
                    OutputToLog = true
                };

                log.Verbose(invocation.ToString());

                var result = commandLineRunner.Execute(invocation);

                return result;
            }
        }
    }
}