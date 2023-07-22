using System;
using System.Collections.Generic;
using System.IO;
using Calamari.Common.Commands;
using Calamari.Common.Features.Packages;
using Calamari.Common.Features.Processes;
using Calamari.Common.Features.Scripting;
using Calamari.Common.Features.Scripts;
using Calamari.Common.Features.StructuredVariables;
using Calamari.Common.Features.Substitutions;
using Calamari.Common.Plumbing.Deployment;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes;
using Calamari.Kubernetes.Integration;

namespace Calamari.Commands
{
    [Command("authenticate-to-kubernetes")]
    public class KubernetesAuthenticationCommand: Command<KubernetesAuthenticationCommandInput>
    {
        readonly ILog log;
        readonly IScriptEngine scriptEngine;
        readonly IVariables variables;
        readonly ICalamariFileSystem fileSystem;
        readonly ICommandLineRunner commandLineRunner;

        public KubernetesAuthenticationCommand(
            ILog log,
            IScriptEngine scriptEngine,
            IVariables variables,
            ICalamariFileSystem fileSystem,
            ICommandLineRunner commandLineRunner)
        {
            this.log = log;
            this.scriptEngine = scriptEngine;
            this.variables = variables;
            this.fileSystem = fileSystem;
            this.commandLineRunner = commandLineRunner;
        }

        protected override void Execute(KubernetesAuthenticationCommandInput inputs)
        {
            var workingDirectory = ".";
            
            var environmentVars = new Dictionary<string, string>();
            var kubectl = new Kubectl(variables, log, commandLineRunner, workingDirectory, environmentVars);
            var setupKubectlAuthentication = new SetupKubectlAuthentication(variables,
                                                                            log,
                                                                            ScriptSyntax.Bash,
                                                                            commandLineRunner,
                                                                            kubectl,
                                                                            environmentVars,
                                                                            workingDirectory);
            var accountType = variables.Get("Octopus.Account.AccountType");

            try
            {
                var result = setupKubectlAuthentication.Execute(accountType);

                if (result.ExitCode != 0)
                {
                    log.Error("Setup authentication failed");
                }
            }
            catch (CommandLineException ex)
            {
                log.Error($"Setup authentication failed with error: {ex.Message}");
                return;
            }
            
            variables.Set("KubeConfig", fileSystem.GetFullPath(environmentVars["KUBECONFIG"]));
        }
    }
    
    public class KubernetesAuthenticationCommandInput { }
}