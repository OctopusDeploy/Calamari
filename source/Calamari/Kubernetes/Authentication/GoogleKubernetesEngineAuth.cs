using System;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.Integration;

namespace Calamari.Kubernetes.Authentication
{
    public class GoogleKubernetesEngineAuth
    {
        readonly GCloud gcloudCli;
        readonly Kubectl kubectlCli;
        readonly IVariables deploymentVariables;

        /// <summary>
        /// This class is responsible for configuring the kubectl auth against a GKE cluster for an Octopus Deployment
        /// </summary>
        /// <param name="gcloudCli"></param>
        /// <param name="kubectlCli"></param>
        /// <param name="deploymentVariables"></param>
        public GoogleKubernetesEngineAuth(GCloud gcloudCli, Kubectl kubectlCli, IVariables deploymentVariables)
        {
            this.gcloudCli = gcloudCli;
            this.kubectlCli = kubectlCli;
            this.deploymentVariables = deploymentVariables;
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

            var gkeClusterName = deploymentVariables.Get(SpecialVariables.GkeClusterName);
            gcloudCli.ConfigureGkeKubeCtlAuthentication(kubectlCli, gkeClusterName, region, zone, @namespace);

            return true;
        }
    }
}