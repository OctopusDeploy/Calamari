using System;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.Integration;

namespace Calamari.Kubernetes.Authentication
{
    public class AzureKubernetesServicesAuth
    {
        readonly AzureCli azureCli;
        readonly Kubectl kubectlCli;
        readonly KubeLogin kubeLogin;
        readonly IVariables deploymentVariables;

        /// <summary>
        /// This class is responsible for configuring the kubectl auth against an AKS cluster for an Octopus Deployment
        /// </summary>
        /// <param name="azureCli"></param>
        /// <param name="kubectlCli"></param>
        /// <param name="KubeLogin"></param>
        /// <param name="deploymentVariables"></param>
        public AzureKubernetesServicesAuth(AzureCli azureCli, Kubectl kubectlCli, KubeLogin kubeloginCli, IVariables deploymentVariables)
        {
            this.azureCli = azureCli;
            this.kubectlCli = kubectlCli;
            this.deploymentVariables = deploymentVariables;
            this.kubeLogin = kubeloginCli;
        }

        public bool TryConfigure(string @namespace, string kubeConfig)
        {
            if (!azureCli.TrySetAz())
                return false;

            var disableAzureCli = deploymentVariables.GetFlag("OctopusDisableAzureCLI");
            if (!disableAzureCli)
            {
                var azEnvironment = deploymentVariables.Get("Octopus.Action.Azure.Environment") ?? "AzureCloud";
                var subscriptionId = deploymentVariables.Get("Octopus.Action.Azure.SubscriptionId");
                var tenantId = deploymentVariables.Get("Octopus.Action.Azure.TenantId");
                var clientId = deploymentVariables.Get("Octopus.Action.Azure.ClientId");
                var password = deploymentVariables.Get("Octopus.Action.Azure.Password");
                azureCli.ConfigureAzAccount(subscriptionId, tenantId, clientId, password, azEnvironment);

                var azureResourceGroup = deploymentVariables.Get("Octopus.Action.Kubernetes.AksClusterResourceGroup");
                var azureCluster = deploymentVariables.Get(SpecialVariables.AksClusterName);
                var azureAdmin = deploymentVariables.GetFlag("Octopus.Action.Kubernetes.AksAdminLogin");
                azureCli.ConfigureAksKubeCtlAuthentication(kubectlCli, azureResourceGroup, azureCluster, @namespace, kubeConfig, azureAdmin);
                kubeLogin.ConfigureAksKubeLogin();
            }

            return true;
        }
    }
}
