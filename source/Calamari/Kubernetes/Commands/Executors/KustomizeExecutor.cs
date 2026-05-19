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
using Octopus.Versioning.Semver;

namespace Calamari.Kubernetes.Commands.Executors
{
    public interface IKustomizeKubernetesApplyExecutor : IKubernetesApplyExecutor
    {
    }
    
    class KustomizeExecutor : BaseKubernetesApplyExecutor, IKustomizeKubernetesApplyExecutor
    {
        public const string HydratedKustomizeManifestFilename = "hydrated-kustomize-manifest.yaml";
        static readonly SemanticVersion MinimumKubectlVersion = new SemanticVersion("1.24.0");
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

            log.Verbose("Validating kubectl version");
            var versionOutput = ValidateKubectlVersion();
            
            log.Info("Building kustomization");
            BuildKustomization(deployment.CurrentDirectory, overlayPath);
            
            log.Verbose("Reporting manifest");
            manifestReporter.ReportManifestFileApplied(HydratedManifestFilepath(deployment.CurrentDirectory));
            
            log.Info("Applying kustomization");
            return await ApplyKustomization(deployment, appliedResourcesCallback, overlayPath);
        }

        async Task<ResourceIdentifier[]> ApplyKustomization(RunningDeployment deployment, Func<ResourceIdentifier[], Task> appliedResourcesCallback, string overlayPath)
        {
            string[] executeArgs = ["apply", "-f", $"\"{HydratedManifestFilepath(deployment.CurrentDirectory)}\"", "-o", "json"];
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
            string[] executeArgs = ["kustomize", $"\"{KustomizationDirectory(currentDirectory, overlayPath)}\"", "-o", $"\"{HydratedManifestFilepath(currentDirectory)}\""];
            
            var commandResult = kubectl.ExecuteCommandAndReturnOutput(executeArgs);
            commandResult.LogErrorsWithSanitizedDirectory(log, currentDirectory);
            if (commandResult.Result.ExitCode != 0)
            {
                throw new KubectlException("Failed to build kustomization");
            }
        }

        KubectlVersionOutput ValidateKubectlVersion()
        {
            var versionOutput = kubectl.GetVersion();
            if (versionOutput == null)
                throw new KubectlException("Could not determine the kubectl version.");

            if (versionOutput.KubectlVersion < MinimumKubectlVersion)
                throw new KubectlException($"kubectl is on version {versionOutput.KubectlVersion}, it needs to be v{MinimumKubectlVersion} or higher to run Kustomize.");

            log.Verbose($"kubectl version: {versionOutput.KubectlVersion}");
            return versionOutput;
        }
    }
}