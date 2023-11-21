using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.Authentication;
using Calamari.Kubernetes.Integration;

namespace Calamari.Kubernetes.Context
{
    public class UsernamePasswordContextProvider
    {
        readonly IVariables variables;
        readonly ILog log;
        readonly IKubectl kubectl;

        public UsernamePasswordContextProvider(IVariables variables,
            ILog log,
            IKubectl kubectl)
        {
            this.variables = variables;
            this.log = log;
            this.kubectl = kubectl;
        }

        public bool TrySetContext(string @namespace, string accountType, string clusterUrl, string clientCert, string skipTlsVerification)
        {
            if (accountType != AccountTypes.UsernamePassword)
                return false;

            if (!new CertificateAuthConfigurationProvider(variables, log, kubectl)
                .TryConfigure(@namespace, clusterUrl, clientCert, skipTlsVerification))
                return false;

            SetupContextForUsernamePassword(KubeContextConstants.User);
            return true;
        }

        void SetupContextForUsernamePassword(string user)
        {
            var username = variables.Get(Deployment.SpecialVariables.Account.Username);
            var password = variables.Get(Deployment.SpecialVariables.Account.Password);
            if (password != null)
            {
                log.AddValueToRedact(password, "<password>");
            }
            kubectl.ExecuteCommandAndAssertSuccess("config", "set-credentials", user, $"--username={username}", $"--password={password}");
        }
    }
}