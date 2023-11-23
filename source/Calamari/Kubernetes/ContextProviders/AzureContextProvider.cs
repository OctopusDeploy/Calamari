using System.Collections.Generic;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.Authentication;
using Calamari.Kubernetes.Integration;

namespace Calamari.Kubernetes.ContextProviders
{
    public class AzureContextProvider
    {
        readonly IVariables variables;
        readonly ILog log;
        readonly ICommandLineRunner commandLineRunner;
        readonly IKubectl kubectl;
        readonly Dictionary<string, string> environmentVars;
        readonly string workingDirectory;

        public AzureContextProvider(IVariables variables,
            ILog log,
            ICommandLineRunner commandLineRunner,
            IKubectl kubectl,
            Dictionary<string, string> environmentVars,
            string workingDirectory)
        {
            this.variables = variables;
            this.log = log;
            this.commandLineRunner = commandLineRunner;
            this.kubectl = kubectl;
            this.environmentVars = environmentVars;
            this.workingDirectory = workingDirectory;
        }

        public bool TrySetContext(string kubeConfig, string @namespace, string accountType, string clusterUrl)
        {
            if (accountType != AccountTypes.AzureServicePrincipal &&
                accountType != AccountTypes.AzureOidc)
                return false;

            var azureCli = new AzureCli(log, commandLineRunner, workingDirectory, environmentVars);
            var kubeloginCli = new KubeLogin(log, commandLineRunner, workingDirectory, environmentVars);
            var azureAuth = new AzureKubernetesServicesAuth(azureCli, kubectl, kubeloginCli, variables);

            return azureAuth.TryConfigure(@namespace, kubeConfig);
        }
    }
}