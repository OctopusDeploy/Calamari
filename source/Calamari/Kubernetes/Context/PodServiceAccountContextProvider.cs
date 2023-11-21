using System.IO;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.Integration;

namespace Calamari.Kubernetes.Context
{
    public class PodServiceAccountContextProvider
    {
        readonly IVariables variables;
        readonly ILog log;
        readonly IKubectl kubectl;
        private readonly ICalamariFileSystem fileSystem;

        public PodServiceAccountContextProvider(IVariables variables,
            ILog log,
            IKubectl kubectl,
            ICalamariFileSystem fileSystem)
        {
            this.variables = variables;
            this.log = log;
            this.kubectl = kubectl;
            this.fileSystem = fileSystem;
        }

        public bool TrySetContext(string @namespace, string accountType, string clusterUrl, string clientCert, string skipTlsVerification)
        {
            if (!string.IsNullOrEmpty(accountType) || !string.IsNullOrEmpty(clientCert))
                return false;

            var serverCertPath = variables.Get(SpecialVariables.CertificateAuthorityPath);

            if (!TryProcessAccountTokenOrCertificate(serverCertPath,
                out var isUsingPodServiceAccount,
                out var podServiceAccountToken,
                out var serverCert))
                return false;

            if (!isUsingPodServiceAccount)
                return false;

            SetupContextUsingPodServiceAccount(@namespace,
                KubeContextConstants.Cluster,
                clusterUrl,
                serverCert,
                skipTlsVerification,
                serverCertPath,
                KubeContextConstants.Context,
                KubeContextConstants.User,
                podServiceAccountToken);

            return true;
        }

        bool TryProcessAccountTokenOrCertificate(string serverCertPath, out bool isUsingPodServiceAccount, out string podServiceAccountToken, out string serverCert)
        {
            isUsingPodServiceAccount = false;
            podServiceAccountToken = null;
            serverCert = null;

            var podServiceAccountTokenPath = variables.Get(SpecialVariables.PodServiceAccountTokenPath);

            if (string.IsNullOrEmpty(podServiceAccountTokenPath) && string.IsNullOrEmpty(serverCertPath))
            {
                log.Error("Kubernetes account type or certificate is missing");
                return false;
            }

            if (!string.IsNullOrEmpty(podServiceAccountTokenPath))
            {
                if (fileSystem.FileExists(podServiceAccountTokenPath))
                {
                    podServiceAccountToken = fileSystem.ReadFile(podServiceAccountTokenPath);
                    if (string.IsNullOrEmpty(podServiceAccountToken))
                    {
                        log.Error("Pod service token file is empty");
                        return false;
                    }

                    isUsingPodServiceAccount = true;
                }
                else
                {
                    log.Error("Pod service token file not found");
                    return false;
                }
            }

            if (!string.IsNullOrEmpty(serverCertPath))
            {
                if (fileSystem.FileExists(serverCertPath))
                {
                    serverCert = fileSystem.ReadFile(serverCertPath);
                }
                else
                {
                    log.Error("Certificate authority file not found");
                    return false;
                }
            }

            return true;
        }

        void SetupContextUsingPodServiceAccount(string @namespace,
            string cluster,
            string clusterUrl,
            string serverCert,
            string skipTlsVerification,
            string serverCertPath,
            string context,
            string user,
            string podServiceAccountToken)
        {
            kubectl.ExecuteCommandAndAssertSuccess("config", "set-cluster", cluster, $"--server={clusterUrl}");

            if (string.IsNullOrEmpty(serverCert))
            {
                kubectl.ExecuteCommandAndAssertSuccess("config", "set-cluster", cluster, $"--insecure-skip-tls-verify={skipTlsVerification}");
            }
            else
            {
                kubectl.ExecuteCommandAndAssertSuccess("config", "set-cluster", cluster, $"--certificate-authority={serverCertPath}");
            }

            kubectl.ExecuteCommandAndAssertSuccess("config", "set-context", context, $"--user={user}", $"--cluster={cluster}", $"--namespace={@namespace}");
            kubectl.ExecuteCommandAndAssertSuccess("config", "use-context", context);

            log.Info($"Creating kubectl context to {clusterUrl} (namespace {@namespace}) using a Pod Service Account Token");
            log.AddValueToRedact(podServiceAccountToken, "<token>");
            kubectl.ExecuteCommandAndAssertSuccess("config", "set-credentials", user, $"--token={podServiceAccountToken}");
        }
    }
}