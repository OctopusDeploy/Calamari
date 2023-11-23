using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.Authentication;
using Calamari.Kubernetes.Integration;

namespace Calamari.Kubernetes.ContextProviders
{
    public class TokenContextProvider
    {
        readonly IVariables variables;
        readonly ILog log;
        readonly IKubectl kubectl;

        public TokenContextProvider(IVariables variables,
            ILog log,
            IKubectl kubectl)
        {
            this.variables = variables;
            this.log = log;
            this.kubectl = kubectl;
        }

        public bool TrySetContext(string @namespace, string accountType, string clusterUrl, string clientCert, string skipTlsVerification)
        {
            if (accountType != AccountTypes.Token)
                return false;

            if (!new CertificateAuthConfigurationProvider(variables, log, kubectl)
                .TryConfigure(@namespace, clusterUrl, clientCert, skipTlsVerification))
                return false;

            var token = variables.Get(Deployment.SpecialVariables.Account.Token);
            if (string.IsNullOrEmpty(token))
            {
                log.Error("Kubernetes authentication Token is missing");
                return false;
            }

            SetupContextForToken(@namespace, token, clusterUrl, KubeContextConstants.User);

            return true;
        }

        void SetupContextForToken(string @namespace, string token, string clusterUrl, string user)
        {
            log.AddValueToRedact(token, "<token>");
            log.Info($"Creating kubectl context to {clusterUrl} (namespace {@namespace}) using a Token");
            kubectl.ExecuteCommandAndAssertSuccess("config", "set-credentials", user, $"--token={token}");
        }
    }
}