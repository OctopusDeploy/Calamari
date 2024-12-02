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
        const string HydratedKustomizeManifestFilename = "hydrated-kustomize-manifest.yaml";
        const int MinimumKubectlVersionMajor = 1;
        const int MinimumKubectlVersionMinor = 24;
        readonly ILog log;
        readonly Kubectl kubectl;
        readonly IManifestReporter manifestReporter;

        string KustomizationDirectory(string currentDirectory, string overlayPath) => Path.Combine(currentDirectory, KubernetesDeploymentCommandBase.PackageDirectoryName, overlayPath);
        string HydratedManifestFilepath(string currentDirectory) => Path.Combine(currentDirectory, HydratedKustomizeManifestFilename);

        public KustomizeExecutor(ILog log, Kubectl kubectl, IManifestReporter manifestReporter) : base(log)
        {
            this.log = log;
            this.kubectl = kubectl;
            this.manifestReporter = manifestReporter;
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
            
            BuildKustomization(deployment.CurrentDirectory, overlayPath);
            
            manifestReporter.ReportManifestFileApplied(HydratedManifestFilepath(deployment.CurrentDirectory));
            
            string[] executeArgs = {"apply", "-f", $@"""{HydratedManifestFilepath(deployment.CurrentDirectory)}""", "-o", "json"};
            executeArgs = executeArgs.AddOptionsForServerSideApply(deployment.Variables, log);
            
            var result = kubectl.ExecuteCommandAndReturnOutput(executeArgs);
            
            var resourceIdentifiers = ProcessKubectlCommandOutput(deployment, result, KustomizationDirectory(deployment.CurrentDirectory, overlayPath)).ToArray();
            
            if (appliedResourcesCallback != null)
            {
                await appliedResourcesCallback(resourceIdentifiers);
            }

            return resourceIdentifiers;
        }

        void BuildKustomization(string currentDirectory, string overlayPath)
        {
            string[] executeArgs = {"kustomize", $@"""{KustomizationDirectory(currentDirectory, overlayPath)}""", "-o", $@"""{HydratedManifestFilepath(currentDirectory)}"""};
            
            var commandResult = kubectl.ExecuteCommandAndReturnOutput(executeArgs);
            commandResult.LogErrorsWithSanitizedDirectory(log, currentDirectory);
            if (commandResult.Result.ExitCode != 0)
            {
                throw new KubectlException("Failed to build kustomization");
            }
        }

        void ValidateKubectlVersion(string currentDirectory)
        {
            var commandResult = kubectl.ExecuteCommandAndReturnOutput("version", "--client", "-o", "json");
            commandResult.LogErrorsWithSanitizedDirectory(log, currentDirectory);
            if (commandResult.Result.ExitCode != 0)
            {
                throw new KubectlException("Failed to check kubectl version");
            }
            
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