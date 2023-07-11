#if !NET40
using System.Collections.Generic;
using Calamari.Common.Commands;
using Calamari.Common.Features.Processes;
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
        private readonly ILog log;
        private readonly IVariables variables;
        private readonly ICalamariFileSystem fileSystem;
        private readonly ICommandLineRunner commandLineRunner;
        private readonly Kubectl kubectl;

        public KubernetesAuthenticationCommand(
            ILog log,
            IVariables variables,
            ICalamariFileSystem fileSystem,
            ICommandLineRunner commandLineRunner,
            Kubectl kubectl)
        {
            this.log = log;
            this.variables = variables;
            this.fileSystem = fileSystem;
            this.commandLineRunner = commandLineRunner;
            this.kubectl = kubectl;
        }

        protected override void Execute(KubernetesAuthenticationCommandInput inputs)
        {
            log.Info("Setting up KubeConfig authentication.");
            var environmentVars = new Dictionary<string, string>();
            var runningDeployment = new RunningDeployment(variables);
            
            kubectl.SetEnvironmentVariables(environmentVars);
            kubectl.SetWorkingDirectory(runningDeployment.CurrentDirectory);
            
            var setupKubectlAuthentication = new SetupKubectlAuthentication(variables,
                log,
                commandLineRunner,
                kubectl,
                environmentVars,
                runningDeployment.CurrentDirectory);
            var accountType = variables.Get("Octopus.Account.AccountType");

            try
            {
                var result = setupKubectlAuthentication.Execute(accountType);
                result.VerifySuccess();
            }
            catch (CommandLineException ex)
            {
                log.Error("KubeConfig authentication failed.");
                throw new CommandException(ex.Message);
            }
            
            variables.Set("Octopus.KubeConfig.Path", fileSystem.GetFullPath(environmentVars["KUBECONFIG"]));
        }
    }
    
    public class KubernetesAuthenticationCommandInput { }
}
#endif