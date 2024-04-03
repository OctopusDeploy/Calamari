#if !NET40
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Logging;
using Calamari.Kubernetes.Integration;
using Calamari.Kubernetes.ResourceStatus.Resources;
using Newtonsoft.Json.Linq;

namespace Calamari.Kubernetes.Commands.Executors
{
    public interface IKustomizeKubernetesApplyExecutor : IKubernetesApplyExecutor
    {
    }
    
    class KustomizeExecutor : BaseKubernetesApplyExecutor, IKustomizeKubernetesApplyExecutor
    {
        const int MinimumKubectlVersionMajor = 1;
        const int MinimumKubectlVersionMinor = 24;
        readonly ILog log;
        readonly Kubectl kubectl;

        public KustomizeExecutor(ILog log, Kubectl kubectl) : base(log, kubectl)
        {
            this.log = log;
            this.kubectl = kubectl;
        }

        protected override async Task<IEnumerable<ResourceIdentifier>> ApplyAndGetResourceIdentifiers(RunningDeployment deployment, Func<ResourceIdentifier[], Task> appliedResourcesCallback = null)
        {
            var variables = deployment.Variables;
            var overlayPath = variables.Get(SpecialVariables.KustomizeOverlayPath);

            if (overlayPath == null)
            {
                throw new KubectlException("Kustomization directory not specified");
            }

            ValidateKubectlVersion(deployment.CurrentDirectory);

            var kustomizationDirectory = Path.Combine(deployment.CurrentDirectory, KubernetesDeploymentCommandBase.PackageDirectoryName, overlayPath);
            var result = kubectl.ExecuteCommandAndReturnOutput("apply", "-k", $@"""{kustomizationDirectory}""", "-o", "json");

            var resourceIdentifiers = ProcessKubectlCommandOutput(deployment, result, kustomizationDirectory).ToArray();
            
            if (appliedResourcesCallback != null)
            {
                await appliedResourcesCallback(resourceIdentifiers);
            }

            return resourceIdentifiers;
        }

        void ValidateKubectlVersion(string currentDirectory)
        {
            var commandResult = kubectl.ExecuteCommandAndReturnOutput("version", "--client", "-o", "json");
            CheckResultForErrors(commandResult, currentDirectory);
            var outputJson = commandResult.Output.InfoLogs.Join(Environment.NewLine);

            if (!TryParseVersion(outputJson, out var major, out var minor))
                throw new KubectlException("Could not determine the kubectl version.");

            if (major < MinimumKubectlVersionMajor || minor < MinimumKubectlVersionMinor)
                throw new KubectlException($"kubectl is on version v{major}.{minor}, it needs to be v{MinimumKubectlVersionMajor}.{MinimumKubectlVersionMinor} or higher to run Kustomize.");
        }

        bool TryParseVersion(string kubectlClientVersionJson, out int major, out int minor)
        {
            try
            {
                var outer = JToken.Parse(kubectlClientVersionJson);
                major = GetVersion(outer, "clientVersion.major");
                minor = GetVersion(outer, "clientVersion.minor");
                return true;
            }
            catch
            {
                major = -1;
                minor = -1;
                return false;
            }
        }

        static int GetVersion(JToken root, string jsonPathToVersion)
        {
            var clientVersionToken = root.SelectToken($"{jsonPathToVersion}");

            if (clientVersionToken != null && int.TryParse(clientVersionToken.ToString(), out var version))
                return version;

            return -1;
        }
    }
}
#endif