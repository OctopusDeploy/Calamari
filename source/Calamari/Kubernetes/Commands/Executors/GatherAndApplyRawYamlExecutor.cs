using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Calamari.Common.Commands;
using Calamari.Common.Plumbing.FileSystem;
using Calamari.Common.Plumbing.Logging;
using Calamari.Common.Plumbing.Variables;
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
        readonly Kubectl kubectl;

        public GatherAndApplyRawYamlExecutor(
            ILog log,
            ICalamariFileSystem fileSystem,
            Kubectl kubectl) : base(log)
        {
            this.log = log;
            this.fileSystem = fileSystem;
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
            if (!LogFoundFiles(globDirectory))
                return Array.Empty<ResourceIdentifier>();

            string[] executeArgs = {"apply", "-f", $@"""{globDirectory.Directory}""", "--recursive", "-o", "json"};
            executeArgs = executeArgs.AddOptionsForServerSideApply(deployment.Variables, log);

            var result = kubectl.ExecuteCommandAndReturnOutput(executeArgs);

            return ProcessKubectlCommandOutput(deployment, result, globDirectory.Directory);
        }

        /// <summary>
        /// Logs files that are found at the relevant glob locations.
        /// </summary>
        /// <param name="globDirectory"></param>
        /// <returns>True if files are found, False if no files exist at this location</returns>
        bool LogFoundFiles(GlobDirectory globDirectory)
        {
            var directoryWithTrailingSlash = globDirectory.Directory + Path.DirectorySeparatorChar;
            var files = fileSystem.EnumerateFilesRecursively(globDirectory.Directory).ToArray();
            if (!files.Any())
            {
                log.Warn($"No files found matching '{globDirectory.Glob}'");
                return false;
            }

            foreach (var file in files)
            {
                log.Verbose($"Matched file: {fileSystem.GetRelativePath(directoryWithTrailingSlash, file)}");
            }

            return true;
        }
    }
}