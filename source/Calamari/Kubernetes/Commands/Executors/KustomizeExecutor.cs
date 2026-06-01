using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.Extensions;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
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
            BuildKustomization(deployment.CurrentDirectory, overlayPath, variables, versionOutput);

            log.Verbose("Reporting manifest");
            manifestReporter.ReportManifestFileApplied(HydratedManifestFilepath(deployment.CurrentDirectory));

            log.Info("Applying kustomization");
            var resourceIdentifiers = await ApplyKustomization(deployment, appliedResourcesCallback, overlayPath);
            
            AppliedResourcesOutputHelper.SetAppliedResourcesOutputVariable(log, deployment, resourceIdentifiers);
            
            return resourceIdentifiers;
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

        void BuildKustomization(string currentDirectory, string overlayPath, IVariables variables, KubectlVersionOutput versionOutput)
        {
            string[] executeArgs = ["kustomize", $"\"{KustomizationDirectory(currentDirectory, overlayPath)}\"", "-o", $"\"{HydratedManifestFilepath(currentDirectory)}\""];

            executeArgs = ConditionallySetLoadRestrictorArg(variables, versionOutput, executeArgs);

            var commandResult = kubectl.ExecuteCommandAndReturnOutput(executeArgs);
            commandResult.LogErrorsWithSanitizedDirectory(log, currentDirectory);
            if (commandResult.Result.ExitCode != 0)
            {
                throw new KubectlException("Failed to build kustomization");
            }
        }

        string[] ConditionallySetLoadRestrictorArg(IVariables variables, KubectlVersionOutput versionOutput, string[] executeArgs)
        {
            if (!variables.GetFlag(SpecialVariables.KustomizeLoadRestrictorNone))
                return executeArgs;

            // Prefer the bundled kustomize version when available, because the load restrictor
            // flag syntax is defined by kustomize rather than kubectl. Fall back to the existing
            // kubectl-based heuristic if the bundled kustomize version is unavailable.
            var usesKustomizeV5Syntax = versionOutput.KustomizeVersion?.Major >= 5
                                        || (versionOutput.KustomizeVersion == null && versionOutput.KubectlVersion.Minor >= 27);
            var loadRestrictorArg = usesKustomizeV5Syntax
                ? "--load-restrictor=LoadRestrictionsNone"
                : "--load_restrictor=none";
            log.Verbose($"Adding load restrictor flag: {loadRestrictorArg}");
            executeArgs = executeArgs.Concat([loadRestrictorArg]).ToArray();

            return executeArgs;
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