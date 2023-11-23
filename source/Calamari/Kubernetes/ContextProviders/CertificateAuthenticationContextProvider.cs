using System;
using System.Text;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.Integration;

namespace Calamari.Kubernetes.ContextProviders
{
    public class CertificateAuthenticationContextProvider
    {
        readonly IVariables variables;
        readonly ILog log;
        readonly IKubectl kubectl;

        public CertificateAuthenticationContextProvider(IVariables variables,
            ILog log,
            IKubectl kubectl)
        {
            this.variables = variables;
            this.log = log;
            this.kubectl = kubectl;
        }

        public bool TrySetContext(
            string @namespace,
            string clusterUrl,
            string clientCert,
            string skipTlsVerification)
        {
            kubectl.ExecuteCommandAndAssertSuccess("config",
                "set-cluster",
                KubeContextConstants.Cluster,
                $"--server={clusterUrl}");
            kubectl.ExecuteCommandAndAssertSuccess("config",
                "set-context",
                KubeContextConstants.Context,
                $"--user={KubeContextConstants.User}",
                $"--cluster={KubeContextConstants.Cluster}",
                $"--namespace={@namespace}");
            kubectl.ExecuteCommandAndAssertSuccess("config", "use-context", KubeContextConstants.Context);

            var clientCertPem = variables.Get(SpecialVariables.CertificatePem(clientCert));
            var clientCertKey = variables.Get(SpecialVariables.PrivateKeyPem(clientCert));
            var certificateAuthority = variables.Get(SpecialVariables.CertificateAuthority);
            var serverCertPem = variables.Get(SpecialVariables.CertificatePem(certificateAuthority));

            if (!string.IsNullOrEmpty(clientCert))
            {
                if (string.IsNullOrEmpty(clientCertPem))
                {
                    log.Error("Kubernetes client certificate does not include the certificate data");
                    return false;
                }

                if (string.IsNullOrEmpty(clientCertKey))
                {
                    log.Error("Kubernetes client certificate does not include the private key data");
                    return false;
                }

                log.Verbose("Encoding client cert key");
                var clientCertKeyEncoded = Convert.ToBase64String(Encoding.ASCII.GetBytes(clientCertKey));
                log.Verbose("Encoding client cert pem");
                var clientCertPemEncoded = Convert.ToBase64String(Encoding.ASCII.GetBytes(clientCertPem));

                // Don't leak the private key in the logs
                log.SetOutputVariable($"{clientCert}.PrivateKeyPemBase64", clientCertKeyEncoded, variables, true);
                log.AddValueToRedact(clientCertKeyEncoded, "<data>");
                log.AddValueToRedact(clientCertPemEncoded, "<data>");
                kubectl.ExecuteCommandAndAssertSuccess("config",
                    "set",
                    $"users.{KubeContextConstants.User}.client-certificate-data",
                    clientCertPemEncoded);
                kubectl.ExecuteCommandAndAssertSuccess("config",
                    "set",
                    $"users.{KubeContextConstants.User}.client-key-data",
                    clientCertKeyEncoded);
            }

            if (!string.IsNullOrEmpty(certificateAuthority))
            {
                if (string.IsNullOrEmpty(serverCertPem))
                {
                    log.Error("Kubernetes server certificate does not include the certificate data");
                    return false;
                }

                var authorityData = Convert.ToBase64String(Encoding.ASCII.GetBytes(serverCertPem));
                log.AddValueToRedact(authorityData, "<data>");
                kubectl.ExecuteCommandAndAssertSuccess("config",
                    "set",
                    $"clusters.{KubeContextConstants.Cluster}.certificate-authority-data",
                    authorityData);
            }
            else
            {
                kubectl.ExecuteCommandAndAssertSuccess("config",
                    "set-cluster",
                    KubeContextConstants.Cluster,
                    $"--insecure-skip-tls-verify={skipTlsVerification}");
            }

            return true;
        }
    }
}