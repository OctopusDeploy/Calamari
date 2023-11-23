using System.Collections.Generic;
using Calamari.Aws.Deployment;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.Authentication;
using Calamari.Kubernetes.Integration;

namespace Calamari.Kubernetes.ContextProviders
{
    public class AwsContextProvider
    {
        readonly IVariables variables;
        readonly ILog log;
        readonly ICommandLineRunner commandLineRunner;
        readonly IKubectl kubectl;
        readonly Dictionary<string, string> environmentVars;
        readonly string workingDirectory;

        public AwsContextProvider(
            IVariables variables,
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

        public bool TrySetContext(string @namespace, string accountType, string clusterUrl, string clientCert, string skipTlsVerification)
        {
            var eksUseInstanceRole = variables.GetFlag(AwsSpecialVariables.Authentication.UseInstanceRole);
            if (accountType != AccountTypes.AmazonWebServicesAccount && !eksUseInstanceRole)
                return false;

            if (!new CertificateAuthenticationContextProvider(variables, log, kubectl)
                .TrySetContext(@namespace, clusterUrl, clientCert, skipTlsVerification))
                return false;

            new AwsKubernetesAuth(variables, log, commandLineRunner, kubectl, environmentVars, workingDirectory)
                .SetupContextForAmazonServiceAccount(@namespace, clusterUrl, KubeContextConstants.User);
            return true;
        }
    }
}