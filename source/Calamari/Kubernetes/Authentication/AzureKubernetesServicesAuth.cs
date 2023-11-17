using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.Integration;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Kubernetes.Authentication
{
    public class AzureKubernetesServicesAuth
    {
        readonly AzureCli azureCli;
        readonly IKubectl kubectlCli;
        readonly KubeLogin kubeLogin;
        readonly IVariables deploymentVariables;

        /// <summary>
        /// This class is responsible for configuring the kubectl auth against an AKS cluster for an Octopus Deployment
        /// </summary>
        /// <param name="azureCli"></param>
        /// <param name="kubectlCli"></param>
        /// <param name="KubeLogin"></param>
        /// <param name="deploymentVariables"></param>
        public AzureKubernetesServicesAuth(AzureCli azureCli, IKubectl kubectlCli, KubeLogin kubeloginCli, IVariables deploymentVariables)
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

            if (FeatureToggle.KubernetesAksKubeloginFeatureToggle.IsEnabled(deploymentVariables))
            {
                kubeLogin.TrySetKubeLogin();
            }

            var disableAzureCli = deploymentVariables.GetFlag("OctopusDisableAzureCLI");
            if (!disableAzureCli)
            {
                var azEnvironment = deploymentVariables.Get(Deployment.SpecialVariables.Action.Azure.Environment) ?? "AzureCloud";
                var subscriptionId = deploymentVariables.Get(Deployment.SpecialVariables.Action.Azure.SubscriptionId);
                var tenantId = deploymentVariables.Get(Deployment.SpecialVariables.Action.Azure.TenantId);
                var clientId = deploymentVariables.Get(Deployment.SpecialVariables.Action.Azure.ClientId);
                var password = deploymentVariables.Get(Deployment.SpecialVariables.Action.Azure.Password);
                var jwt = deploymentVariables.Get(Deployment.SpecialVariables.Action.Azure.Jwt);

                var isOidc = !jwt.IsNullOrEmpty();

                var credential = isOidc ? jwt : password;

                azureCli.ConfigureAzAccount(subscriptionId, tenantId, clientId, credential, azEnvironment, isOidc);

                var azureResourceGroup = deploymentVariables.Get(SpecialVariables.AksClusterResourceGroup);
                var azureCluster = deploymentVariables.Get(SpecialVariables.AksClusterName);
                var azureAdmin = deploymentVariables.GetFlag(SpecialVariables.AksAdminLogin);
                azureCli.ConfigureAksKubeCtlAuthentication(kubectlCli, azureResourceGroup, azureCluster, @namespace, kubeConfig, azureAdmin);
                if (FeatureToggle.KubernetesAksKubeloginFeatureToggle.IsEnabled(deploymentVariables) && kubeLogin.IsConfigured)
                {
                    kubeLogin.ConfigureAksKubeLogin(kubeConfig);
                }
            }

            return true;
        }
    }
}