using System.Collections.Generic;
using Calamari.Common.Features.Processes;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
using Calamari.Kubernetes.Authentication;
using Calamari.Kubernetes.Integration;

namespace Calamari.Kubernetes.ContextProviders
{
    public class GoogleCloudContextProvider
    {
        readonly IVariables variables;
        readonly ILog log;
        readonly ICommandLineRunner commandLineRunner;
        readonly IKubectl kubectl;
        private readonly ICalamariFileSystem fileSystem;
        readonly Dictionary<string,string> environmentVars;
        readonly string workingDirectory;

        public GoogleCloudContextProvider(IVariables variables,
            ILog log,
            ICommandLineRunner commandLineRunner,
            IKubectl kubectl,
            ICalamariFileSystem fileSystem,
            Dictionary<string, string> environmentVars,
            string workingDirectory)
        {
            this.variables = variables;
            this.log = log;
            this.commandLineRunner = commandLineRunner;
            this.kubectl = kubectl;
            this.fileSystem = fileSystem;
            this.environmentVars = environmentVars;
            this.workingDirectory = workingDirectory;
        }

        public bool TrySetContext(string @namespace, string accountType, string clusterUrl)
        {
            var useVmServiceAccount = variables.GetFlag(Deployment.SpecialVariables.Action.GoogleCloud.UseVmServiceAccount);
            var isUsingGoogleCloudAuth = accountType == AccountTypes.GoogleCloudAccount || useVmServiceAccount;
            if (!isUsingGoogleCloudAuth) return false;

            var gcloudCli = new GCloud(log, commandLineRunner, fileSystem, workingDirectory, environmentVars);
            var gkeGcloudAuthPlugin = new GkeGcloudAuthPlugin(log, commandLineRunner, workingDirectory, environmentVars);
            var gcloudAuth = new GoogleKubernetesEngineAuth(gcloudCli, gkeGcloudAuthPlugin, kubectl, variables, log);

            return gcloudAuth.TryConfigure(useVmServiceAccount, @namespace);
        }
    }
}