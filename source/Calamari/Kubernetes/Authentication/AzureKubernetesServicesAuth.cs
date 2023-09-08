using System.Threading.Tasks;
using Calamari.Common.FeatureToggles;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.Integration;
using Octopus.CoreUtilities.Extensions;
#if !NET40
using Microsoft.Identity.Client;
#endif

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

            if (FeatureToggle.KubernetesAksKubeloginFeatureToggle.IsEnabled(deploymentVariables))
            {
                kubeLogin.TrySetKubeLogin();
            }

            var disableAzureCli = deploymentVariables.GetFlag("OctopusDisableAzureCLI");
            if (!disableAzureCli)
            {
                var azEnvironment = deploymentVariables.Get("Octopus.Action.Azure.Environment") ?? "AzureCloud";
                var subscriptionId = deploymentVariables.Get("Octopus.Action.Azure.SubscriptionId");
                var tenantId = deploymentVariables.Get("Octopus.Action.Azure.TenantId");
                var clientId = deploymentVariables.Get("Octopus.Action.Azure.ClientId");
                var password = deploymentVariables.Get("Octopus.Action.Azure.Password");
                var jwt = deploymentVariables.Get("Octopus.OpenIdConnect.Jwt");

                var isOidc = !jwt.IsNullOrEmpty();
#if !NET40
                var resourceManagementEndpointBaseUri = deploymentVariables.Get("Octopus.Action.Azure.ResourceManagementEndPoint", GetDefaultScope(azEnvironment));
                var activeDirectoryEndpointBaseUri = deploymentVariables.Get("Octopus.Action.Azure.ActiveDirectoryEndPoint", "https://login.microsoftonline.com/");

                var token = isOidc ? GetAuthorizationToken(tenantId, clientId, jwt, resourceManagementEndpointBaseUri, activeDirectoryEndpointBaseUri).GetAwaiter().GetResult() : password;
#else
                var token = password;
#endif

                azureCli.ConfigureAzAccount(subscriptionId, tenantId, clientId, token, azEnvironment, isOidc);

                var azureResourceGroup = deploymentVariables.Get("Octopus.Action.Kubernetes.AksClusterResourceGroup");
                var azureCluster = deploymentVariables.Get(SpecialVariables.AksClusterName);
                var azureAdmin = deploymentVariables.GetFlag("Octopus.Action.Kubernetes.AksAdminLogin");
                azureCli.ConfigureAksKubeCtlAuthentication(kubectlCli, azureResourceGroup, azureCluster, @namespace, kubeConfig, azureAdmin);
                if (FeatureToggle.KubernetesAksKubeloginFeatureToggle.IsEnabled(deploymentVariables) && kubeLogin.IsConfigured)
                {
                    kubeLogin.ConfigureAksKubeLogin(kubeConfig);
                }
            }

            return true;
        }

        static string GetDefaultScope(string environmentName)
        {
            switch (environmentName)
            {

                case "AzureChinaCloud":
                    return "https://management.chinacloudapi.cn/.default";
                case "AzureGermanCloud":
                    return "https://management.microsoftazure.de/.default";
                case "AzureUSGovernment":
                    return "https://management.usgovcloudapi.net/.default";
                case "AzureGlobalCloud":
                case "AzureCloud":
                default:
                    // The double slash is intentional for public cloud.
                    return "https://management.azure.com//.default";
            }
        }
        
#if !NET40
        public static async Task<string> GetAuthorizationToken(string tenantId,
                                                               string applicationId,
                                                               string token,
                                                               string managementEndPoint,
                                                               string activeDirectoryEndPoint)
        {
            var authContext = GetOidcContextUri(activeDirectoryEndPoint, tenantId);
            Log.Verbose($"Authentication Context: {authContext}");

            var app = ConfidentialClientApplicationBuilder.Create(applicationId)
                                                          .WithClientAssertion(token)
                                                          .WithAuthority(authContext)
                                                          .Build();

            var result = await app.AcquireTokenForClient(
                                                         new[] { managementEndPoint })
                                  .WithTenantId(tenantId)
                                  .ExecuteAsync()
                                  .ConfigureAwait(false);
            return result.AccessToken;
        }

        static string GetOidcContextUri(string activeDirectoryEndPoint, string tenantId)
        {
            if (!activeDirectoryEndPoint.EndsWith("/"))
            {
                return $"{activeDirectoryEndPoint}/{tenantId}/v2.0";
            }

            return $"{activeDirectoryEndPoint}{tenantId}/v2.0";
        }
#endif
    }
}
