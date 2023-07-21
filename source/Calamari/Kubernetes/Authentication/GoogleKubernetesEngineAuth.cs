using System;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.Integration;
using Octopus.CoreUtilities;
using Octopus.Versioning.Semver;

namespace Calamari.Kubernetes.Authentication
{
    public class GoogleKubernetesEngineAuth
    {
        readonly GCloud gcloudCli;
        readonly GkeGcloudAuthPlugin authPluginCli;
        readonly Kubectl kubectlCli;
        readonly IVariables deploymentVariables;
        readonly ILog log;

        /// <summary>
        /// This class is responsible for configuring the kubectl auth against a GKE cluster for an Octopus Deployment
        /// </summary>
        /// <param name="gcloudCli"></param>
        /// <param name="authPluginCli"></param>
        /// <param name="kubectlCli"></param>
        /// <param name="deploymentVariables"></param>
        public GoogleKubernetesEngineAuth(GCloud gcloudCli,GkeGcloudAuthPlugin authPluginCli, Kubectl kubectlCli, IVariables deploymentVariables, ILog log)
        {
            this.gcloudCli = gcloudCli;
            this.authPluginCli = authPluginCli;
            this.kubectlCli = kubectlCli;
            this.deploymentVariables = deploymentVariables;
            this.log = log;
        }

        public bool TryConfigure(bool useVmServiceAccount, string @namespace)
        {
            if (!gcloudCli.TrySetGcloud())
                return false;

            var accountVariable = deploymentVariables.Get("Octopus.Action.GoogleCloudAccount.Variable");
            var jsonKey = deploymentVariables.Get($"{accountVariable}.JsonKey");
            if (string.IsNullOrEmpty(accountVariable) || string.IsNullOrEmpty(jsonKey))
            {
                jsonKey = deploymentVariables.Get("Octopus.Action.GoogleCloudAccount.JsonKey");
            }

            string impersonationEmails = null;
            if (deploymentVariables.GetFlag("Octopus.Action.GoogleCloud.ImpersonateServiceAccount"))
            {
                impersonationEmails = deploymentVariables.Get("Octopus.Action.GoogleCloud.ServiceAccountEmails");
            }

            var project = deploymentVariables.Get("Octopus.Action.GoogleCloud.Project") ?? string.Empty;
            var region = deploymentVariables.Get("Octopus.Action.GoogleCloud.Region") ?? string.Empty;
            var zone = deploymentVariables.Get("Octopus.Action.GoogleCloud.Zone") ?? string.Empty;
            gcloudCli.ConfigureGcloudAccount(project, region, zone, jsonKey, useVmServiceAccount, impersonationEmails);

            WarnCustomersAboutAuthToolingRequirements();
            var gkeClusterName = deploymentVariables.Get(SpecialVariables.GkeClusterName);
            var useClusterInternalIp = deploymentVariables.GetFlag(SpecialVariables.GkeUseClusterInternalIp);
            gcloudCli.ConfigureGkeKubeCtlAuthentication(kubectlCli, gkeClusterName, region, zone, @namespace, useClusterInternalIp);

            return true;
        }

        /// <summary>
        /// Provide a clear warning in the deployment logs if the customer's environment has a known incompatible combination of tooling
        /// </summary>
        /// <remarks>
        /// From Kubectl 1.26 onward, the `gke-gcloud-auth-plugin` is required to be available on the path.
        /// Without it, generating the `kubeconfig` will succeed, but authentication will fail.
        /// </remarks>
        void WarnCustomersAboutAuthToolingRequirements()
        {
            var kubeCtlVersion = kubectlCli.GetVersion();
            if (kubeCtlVersion.None() || kubeCtlVersion.Value < new SemanticVersion("1.26.0"))
                return;

            if (!authPluginCli.ExistsOnPath())
                log.Warn("From kubectl v1.26 onward, the gke-gcloud-auth-plugin needs to be available on the PATH to authenticate against GKE clusters. See https://oc.to/KubectlAuthChangesGke for more information.");
        }
    }
}