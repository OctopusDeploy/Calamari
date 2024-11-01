using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Kubernetes.Integration;
using Calamari.Kubernetes.ResourceStatus.Resources;
using Octopus.CoreUtilities.Extensions;

namespace Calamari.Kubernetes.Commands.Executors
{
    public interface IRawYamlKubernetesApplyExecutor : IKubernetesApplyExecutor
    {
    }

    class GatherAndApplyRawYamlExecutor : BaseKubernetesApplyExecutor, IRawYamlKubernetesApplyExecutor
    {
        readonly ILog log;
        readonly ICalamariFileSystem fileSystem;
        readonly IManifestReporter manifestReporter;
        readonly Kubectl kubectl;

        public GatherAndApplyRawYamlExecutor(
            ILog log,
            ICalamariFileSystem fileSystem,
            IManifestReporter manifestReporter,
            Kubectl kubectl) : base(log)
        {
            this.log = log;
            this.fileSystem = fileSystem;
            this.manifestReporter = manifestReporter;
            this.kubectl = kubectl;
        }

        protected override async Task<IEnumerable<ResourceIdentifier>> ApplyAndGetResourceIdentifiers(RunningDeployment deployment, Func<ResourceIdentifier[], Task> appliedResourcesCallback = null)
        {
            var variables = deployment.Variables;
            var globs = variables.GetPaths(SpecialVariables.CustomResourceYamlFileName);

            if (globs.IsNullOrEmpty())
                return Enumerable.Empty<ResourceIdentifier>();

            var globberGrouping = new GlobberGrouping(fileSystem);
            var globDirectories = globberGrouping.Group(deployment.CurrentDirectory, globs);
            variables.Set(SpecialVariables.GroupedYamlDirectories, string.Join(";", globDirectories.Select(d => d.Directory)));

            var resourcesIdentifiers = new HashSet<ResourceIdentifier>();
            for (int i = 0; i < globDirectories.Count(); i++)
            {
                var directory = globDirectories[i];
                log.Info($"Applying Batch #{i+1} for YAML matching '{directory.Glob}'");
                var res = ApplyBatchAndReturnResourceIdentifiers(deployment, directory).ToArray();

                if (appliedResourcesCallback != null)
                {
                    await appliedResourcesCallback(res);
                }
                
                resourcesIdentifiers.UnionWith(res);
            }

            return resourcesIdentifiers;
        }

        IEnumerable<ResourceIdentifier> ApplyBatchAndReturnResourceIdentifiers(RunningDeployment deployment, GlobDirectory globDirectory)
        {
            var files = fileSystem.EnumerateFilesRecursively(globDirectory.Directory).ToArray();
            if (!files.Any())
            {
                log.Warn($"No files found matching '{globDirectory.Glob}'");
                return Array.Empty<ResourceIdentifier>();
            }
            
            ReportEachManifestBeingApplied(globDirectory, files);

            string[] executeArgs = {"apply", "-f", $@"""{globDirectory.Directory}""", "--recursive", "-o", "json"};
            executeArgs = executeArgs.AddOptionsForServerSideApply(deployment.Variables, log);
            var result = kubectl.ExecuteCommandAndReturnOutput(executeArgs);

            return ProcessKubectlCommandOutput(deployment, result, globDirectory.Directory);
        }

        void ReportEachManifestBeingApplied(GlobDirectory globDirectory, string[] files)
        {
            var directoryWithTrailingSlash = globDirectory.Directory + Path.DirectorySeparatorChar;
            var namespacedApiResourceDict = GetNamespacedApiResourceDictionary();
            foreach (var file in files)
            {
                var fullFilePath = fileSystem.GetRelativePath(directoryWithTrailingSlash, file);
                log.Verbose($"Matched file: {fullFilePath}");
                manifestReporter.ReportManifestApplied(file, namespacedApiResourceDict);
            }
        }

        Dictionary<ApiResourceIdentifier, bool> GetNamespacedApiResourceDictionary()
        {
            var apiResourceLines = kubectl.ExecuteCommandAndReturnOutput("api-resources");
            apiResourceLines.Result.VerifySuccess();

            return apiResourceLines
                                     .Output.InfoLogs.Skip(1)
                                     .Select(line => line.Split(Array.Empty<char>(), StringSplitOptions.RemoveEmptyEntries).Reverse().ToArray())
                                     .Where(parts => parts.Length > 3)
                                     .ToDictionary( parts => new ApiResourceIdentifier(parts[2], parts[0]), parts => bool.Parse(parts[1]));
        }
    }
}